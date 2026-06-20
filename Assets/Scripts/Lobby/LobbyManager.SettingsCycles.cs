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
    void CycleRoundDuration()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        float current = GetRoundDuration();
        int index = System.Array.FindIndex(RoundDurationOptions, option => Mathf.Abs(option - current) < 0.01f);
        if (index < 0)
            index = 0;

        int nextIndex = (index + 1) % RoundDurationOptions.Length;

        Hashtable props = new Hashtable();
        props[RoomSettings.RoundDurationKey] = RoundDurationOptions[nextIndex];
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleObstacleDensity()
    {
        CycleDensitySetting(RoomSettings.ObstacleDensityKey, GetObstacleDensity());
    }

    void CycleObstacleDestroyEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.ObstacleDestroyEnabledKey] = !AreObstaclesDestructible();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleObstacleHp()
    {
        CycleIntSetting(RoomSettings.ObstacleHpKey, ObstacleHpOptions, GetObstacleHp(), RoomSettings.DefaultObstacleHp);
    }

    void CycleObstacleSizePercent()
    {
        CycleIntSetting(RoomSettings.ObstacleSizePercentKey, ObstacleSizePercentOptions, GetObstacleSizePercent(), RoomSettings.DefaultObstacleSizePercent);
    }

    void CycleObstacleNoBorders()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.ObstacleNoBordersKey] = !AreObstaclesBorderless();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleMapSize()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        string current = GetMapSize();
        int index = System.Array.IndexOf(MapSizeOptions, current);
        if (index < 0)
            index = 1;

        int nextIndex = (index + 1) % MapSizeOptions.Length;

        Hashtable props = new Hashtable();
        props[RoomSettings.MapSizeKey] = MapSizeOptions[nextIndex];
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleToxicBordersEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.ToxicBordersEnabledKey] = !RoomSettings.AreToxicBordersEnabled();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        GameVisualTheme.RequestRuntimeRefresh();
        RefreshHostSettingsUi();
    }

    void CycleMapBackground()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        int current = GetMapBackground();
        int index = System.Array.IndexOf(MapBackgroundOptions, current);
        if (index < 0)
            index = System.Array.IndexOf(MapBackgroundOptions, RoomSettings.DefaultMapBackground);
        if (index < 0)
            index = 0;

        int nextIndex = (index + 1) % MapBackgroundOptions.Length;

        Hashtable props = new Hashtable();
        props[RoomSettings.MapBackgroundKey] = MapBackgroundOptions[nextIndex];
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleVisualEffectsEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.VisualEffectsEnabledKey] = !AreVisualEffectsEnabled();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleAdvancedSpawnVfxEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.AdvancedSpawnVfxEnabledKey] = !RoomSettings.IsAdvancedSpawnVfxEnabled();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleLowHpHullSparksEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.LowHpHullSparksEnabledKey] = !RoomSettings.AreLowHpHullSparksEnabled();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleBoomVfxEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.BoomVfxEnabledKey] = !RoomSettings.AreBoomVfxEnabled();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleDynamicCameraZoomEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.DynamicCameraZoomEnabledKey] = !IsDynamicCameraZoomEnabled();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleParallaxBackground()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        string current = RoomSettings.GetParallaxBackgroundId();
        int index = System.Array.IndexOf(ParallaxBackgroundOptions, current);
        if (index < 0)
            index = System.Array.IndexOf(ParallaxBackgroundOptions, RoomSettings.DefaultParallaxBackground);
        if (index < 0)
            index = 0;

        int nextIndex = (index + 1) % ParallaxBackgroundOptions.Length;

        Hashtable props = new Hashtable();
        props[RoomSettings.ParallaxBackgroundKey] = ParallaxBackgroundOptions[nextIndex];
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        GameVisualTheme.RequestRuntimeRefresh();
        RefreshHostSettingsUi();
    }

    void CycleBackgroundObject()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        string current = RoomSettings.GetBackgroundObjectId();
        int index = System.Array.IndexOf(BackgroundObjectOptions, current);
        if (index < 0)
            index = System.Array.IndexOf(BackgroundObjectOptions, RoomSettings.DefaultBackgroundObject);
        if (index < 0)
            index = 0;

        int nextIndex = (index + 1) % BackgroundObjectOptions.Length;

        Hashtable props = new Hashtable();
        props[RoomSettings.BackgroundObjectKey] = BackgroundObjectOptions[nextIndex];
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        GameVisualTheme.RequestRuntimeRefresh();
        RefreshHostSettingsUi();
    }

    void CycleGravityWellPhysicsEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.GravityWellPhysicsEnabledKey] = !RoomSettings.IsGravityWellPhysicsEnabled();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleEndDisasterMode()
    {
        CycleStringSetting(
            RoomSettings.EndDisasterModeKey,
            EndDisasterModeOptions,
            GetEndDisasterMode(),
            RoomSettings.DefaultEndDisasterMode);
    }

    void CycleEndDisasterWarningSeconds()
    {
        CycleIntSetting(
            RoomSettings.EndDisasterWarningSecondsKey,
            EndDisasterWarningSecondOptions,
            GetEndDisasterWarningSeconds(),
            RoomSettings.DefaultEndDisasterWarningSeconds);
    }

    void CycleTreasureDensity()
    {
        CycleStringSetting(RoomSettings.TreasureDensityKey, TreasureDensityOptions, GetTreasureDensity(), RoomSettings.DefaultTreasureDensity);
    }

    void CycleRadioactiveTreasureDensity()
    {
        CycleStringSetting(
            RoomSettings.RadioactiveTreasureDensityKey,
            RadioactiveTreasureDensityOptions,
            GetRadioactiveTreasureDensity(),
            RoomSettings.DefaultRadioactiveTreasureDensity);
    }

    void CycleAlienSecretsDensity()
    {
        CycleStringSetting(
            RoomSettings.AlienSecretsDensityKey,
            AlienSecretsDensityOptions,
            GetAlienSecretsDensity(),
            RoomSettings.DefaultAlienSecretsDensity);
    }

    void CycleResourceRichness()
    {
        CycleStringSetting(RoomSettings.ResourceRichnessKey, ResourceRichnessOptions, RoomSettings.GetBaseResourceRichness(), RoomSettings.DefaultResourceRichness);
    }

    void CycleCrazyEnemiesEffect()
    {
        CycleMapEffect(RoomSettings.CrazyEnemiesModeKey, RoomSettings.CrazyEnemiesStartUtcMsKey, RoomSettings.CrazyEnemiesActiveKey);
    }

    void CycleFogOfWarEffect()
    {
        CycleMapEffect(RoomSettings.FogOfWarModeKey, RoomSettings.FogOfWarStartUtcMsKey, RoomSettings.FogOfWarActiveKey);
    }

    void CyclePirateBaseEffect()
    {
        CycleMapEffect(RoomSettings.PirateBaseModeKey, RoomSettings.PirateBaseStartUtcMsKey, RoomSettings.PirateBaseActiveKey);
    }

    void CycleAsteroidShowerEffect()
    {
        CycleMapEffect(RoomSettings.AsteroidShowerModeKey, RoomSettings.AsteroidShowerStartUtcMsKey, RoomSettings.AsteroidShowerActiveKey);
    }

    void CycleMapEffect(string modeKey, string startKey, string activeKey)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        string mode = RoomSettings.GetMapEffectMode(modeKey);
        double nowUtcMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Hashtable props = new Hashtable();
        props[activeKey] = false;

        if (mode == RoomSettings.MapEffectModeOff)
        {
            props[modeKey] = RoomSettings.MapEffectModeAlwaysOn;
            props[startKey] = -1d;
        }
        else if (mode == RoomSettings.MapEffectModeAlwaysOn)
        {
            props[modeKey] = RoomSettings.MapEffectModeUtcStart;
            props[startKey] = GetNextUtcHourStartMs();
        }
        else
        {
            double currentStart = RoomSettings.GetMapEffectStartUtcMs(startKey);
            double nextStart = currentStart >= nowUtcMs ? currentStart + 60d * 60d * 1000d : GetNextUtcHourStartMs();
            if (nextStart > nowUtcMs + 12d * 60d * 60d * 1000d)
            {
                props[modeKey] = RoomSettings.MapEffectModeOff;
                props[startKey] = -1d;
            }
            else
            {
                props[modeKey] = RoomSettings.MapEffectModeUtcStart;
                props[startKey] = nextStart;
            }
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
        RefreshMapDetailsUi();
    }

    void CycleMapEffectChancePercent(string mapId, string ruleId)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        int current = RoomSettings.GetMapEffectChancePercent(mapId, ruleId);
        int next = GetNextMapEffectChancePercent(current);
        Hashtable props = new Hashtable
        {
            [RoomSettings.GetMapEffectChanceKey(mapId, ruleId)] = next
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    int GetNextMapEffectChancePercent(int current)
    {
        current = RoomSettings.NormalizeMapEffectChancePercent(current);
        for (int i = 0; i < MapEffectChancePercentOptions.Length; i++)
        {
            if (MapEffectChancePercentOptions[i] > current)
                return MapEffectChancePercentOptions[i];
        }

        return MapEffectChancePercentOptions[0];
    }

    double GetNextUtcHourStartMs()
    {
        System.DateTimeOffset now = System.DateTimeOffset.UtcNow;
        System.DateTimeOffset nextHour = new System.DateTimeOffset(
            now.Year,
            now.Month,
            now.Day,
            now.Hour,
            0,
            0,
            System.TimeSpan.Zero).AddHours(1d);
        return nextHour.ToUnixTimeMilliseconds();
    }

    void CycleSpaceJunkDensity()
    {
        CycleStringSetting(RoomSettings.SpaceJunkDensityKey, SpaceJunkDensityOptions, GetSpaceJunkDensity(), RoomSettings.DefaultSpaceJunkDensity);
    }

    void CycleContainersDensity()
    {
        CycleStringSetting(RoomSettings.ContainersDensityKey, ContainersDensityOptions, GetContainersDensity(), RoomSettings.DefaultContainersDensity);
    }

    void CycleArtifactAsteroidsDensity()
    {
        CycleStringSetting(RoomSettings.ArtifactAsteroidsDensityKey, ArtifactAsteroidsDensityOptions, GetArtifactAsteroidsDensity(), RoomSettings.DefaultArtifactAsteroidsDensity);
    }

    void CycleRandomLootWreckCount()
    {
        CycleIntSetting(
            RoomSettings.RandomLootWreckCountKey,
            RandomLootWreckCountOptions,
            GetRandomLootWreckCount(),
            RoomSettings.DefaultRandomLootWreckCount);
    }

    void CycleHiddenTreasureEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.HiddenTreasureEnabledKey] = !RoomSettings.IsHiddenTreasureEnabled();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleNebulaDensity()
    {
        CycleDensitySetting(RoomSettings.NebulaDensityKey, GetNebulaDensity());
    }

    void CycleFireNebulaDensity()
    {
        CycleDensitySetting(RoomSettings.FireNebulaDensityKey, GetFireNebulaDensity());
    }

    void CycleToxicNebulaDensity()
    {
        CycleDensitySetting(RoomSettings.ToxicNebulaDensityKey, GetToxicNebulaDensity());
    }

    void CycleNebulaSize()
    {
        CycleStringSetting(RoomSettings.NebulaSizeKey, NebulaSizeOptions, GetNebulaSize(), RoomSettings.DefaultNebulaSize);
    }

    void CycleFireNebulaSize()
    {
        CycleStringSetting(RoomSettings.FireNebulaSizeKey, NebulaSizeOptions, GetFireNebulaSize(), RoomSettings.DefaultFireNebulaSize);
    }

    void CycleToxicNebulaSize()
    {
        CycleStringSetting(RoomSettings.ToxicNebulaSizeKey, NebulaSizeOptions, GetToxicNebulaSize(), RoomSettings.DefaultToxicNebulaSize);
    }

    void CycleAdvancedNebulaEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.AdvancedNebulaEnabledKey] = !RoomSettings.IsAdvancedNebulaEnabled();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleCloudsDensity()
    {
        CycleDensitySetting(RoomSettings.CloudsDensityKey, GetCloudsDensity());
    }

    void CycleCloudsSize()
    {
        CycleStringSetting(RoomSettings.CloudsSizeKey, NebulaSizeOptions, GetCloudsSize(), RoomSettings.DefaultCloudsSize);
    }

    void CycleExtractionCount()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        int current = GetExtractionCount();
        int index = System.Array.IndexOf(ExtractionCountOptions, current);
        if (index < 0)
            index = 2;

        int nextIndex = (index + 1) % ExtractionCountOptions.Length;

        Hashtable props = new Hashtable();
        props[RoomSettings.ExtractionCountKey] = ExtractionCountOptions[nextIndex];
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleExtractionType()
    {
        CycleStringSetting(
            RoomSettings.ExtractionTypeKey,
            ExtractionTypeOptions,
            GetExtractionType(),
            RoomSettings.DefaultExtractionType);
    }

    void CycleRepairBayCount()
    {
        CycleIntSetting(
            RoomSettings.RepairBayCountKey,
            RepairBayCountOptions,
            GetRepairBayCount(),
            RoomSettings.DefaultRepairBayCount);
    }

    void CycleSpaceFactoryCount()
    {
        CycleIntSetting(
            RoomSettings.SpaceFactoryCountKey,
            SpaceFactoryCountOptions,
            GetSpaceFactoryCount(),
            RoomSettings.DefaultSpaceFactoryCount);
    }

    void CycleScienceStationCount()
    {
        CycleIntSetting(
            RoomSettings.ScienceStationCountKey,
            ScienceStationCountOptions,
            GetScienceStationCount(),
            RoomSettings.DefaultScienceStationCount);
    }

    void CycleAvengerPlotEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.AvengerPlotEnabledKey] = !RoomSettings.IsAvengerPlotEnabled();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleViperPlotChancePercent()
    {
        CycleIntSetting(
            RoomSettings.ViperPlotChancePercentKey,
            ViperPlotChancePercentOptions,
            RoomSettings.GetViperPlotChancePercent(),
            LobbyMapCatalog.GetDefaultViperPlotChancePercent(RoomSettings.GetSelectedLobbyMapId()));
    }

    void CycleArrowPlotChancePercent()
    {
        CycleIntSetting(
            RoomSettings.ArrowPlotChancePercentKey,
            ArrowPlotChancePercentOptions,
            RoomSettings.GetArrowPlotChancePercent(),
            LobbyMapCatalog.GetDefaultArrowPlotChancePercent(RoomSettings.GetSelectedLobbyMapId()));
    }

    void CycleBisonPlotChancePercent()
    {
        CycleIntSetting(
            RoomSettings.BisonPlotChancePercentKey,
            BisonPlotChancePercentOptions,
            RoomSettings.GetBisonPlotChancePercent(),
            LobbyMapCatalog.GetDefaultBisonPlotChancePercent(RoomSettings.GetSelectedLobbyMapId()));
    }

    void CycleInvaderPlotChancePercent()
    {
        CycleIntSetting(
            RoomSettings.InvaderPlotChancePercentKey,
            InvaderPlotChancePercentOptions,
            RoomSettings.GetInvaderPlotChancePercent(),
            LobbyMapCatalog.GetDefaultInvaderPlotChancePercent(RoomSettings.GetSelectedLobbyMapId()));
    }

    void CycleBoosterSlowdown()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        int current = GetBoosterSlowdownPercent();
        int index = System.Array.IndexOf(BoosterSlowdownOptions, current);
        if (index < 0)
            index = 0;

        int nextIndex = (index + 1) % BoosterSlowdownOptions.Length;

        Hashtable props = new Hashtable();
        props[RoomSettings.BoosterSlowdownKey] = BoosterSlowdownOptions[nextIndex];
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleBoosterRecoveryDelay()
    {
        CycleIntSetting(RoomSettings.BoosterRecoveryDelayKey, BoosterRecoveryDelayOptions, GetBoosterRecoveryDelay(), 0);
    }

    void CycleAdvancedBoosterEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.AdvancedBoosterEnabledKey] = !RoomSettings.IsAdvancedBoosterEnabled();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleShipDriftEnabled()
    {
        RefreshHostSettingsUi();
    }

    void CycleLastShipTimerMultiplier()
    {
        CycleFloatSetting(RoomSettings.LastShipTimerMultiplierKey, LastShipTimerMultiplierOptions, GetLastShipTimerMultiplier(), RoomSettings.DefaultLastShipTimerMultiplier);
    }

    void CycleInventoryLossEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.InventoryLossEnabledKey] = !RoomSettings.IsInventoryLossEnabled();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleEquipmentLossEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.EquipmentLossEnabledKey] = !RoomSettings.IsEquipmentLossEnabled();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleCosmicWormEffect()
    {
        CycleMapEffect(RoomSettings.CosmicWormModeKey, RoomSettings.CosmicWormStartUtcMsKey, RoomSettings.CosmicWormActiveKey);
    }

    void CycleMovingObjectsEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        string current = GetMovingObjectsMode();
        int index = System.Array.IndexOf(MovingObjectsModeOptions, current);
        if (index < 0)
            index = 0;

        int nextIndex = (index + 1) % MovingObjectsModeOptions.Length;

        Hashtable props = new Hashtable();
        props[RoomSettings.MovingObjectsEnabledKey] = MovingObjectsModeOptions[nextIndex];
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleEnemyBotsEnabled()
    {
        CycleEnemyEnabled(EnemyBotKind.Drone);
    }

    void CycleCorsairEnabled()
    {
        CycleEnemyEnabled(EnemyBotKind.Corsair);
    }

    void CycleEnemyEnabled(EnemyBotKind kind)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return;

        Hashtable props = new Hashtable();
        bool nextValue = !RoomSettings.GetEnemyEnabled(kind);
        props[definition.EnabledRoomKey] = nextValue;
        if (kind == EnemyBotKind.Drone)
            props[RoomSettings.EnemyBotsEnabledKey] = nextValue;
        else if (kind == EnemyBotKind.Corsair)
            props[RoomSettings.CorsairEnabledKey] = nextValue;
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleCorsairSpawnSecond()
    {
        CycleEnemySpawnSecond(EnemyBotKind.Corsair);
    }

    void CycleCorsairHp()
    {
        CycleEnemyHp(EnemyBotKind.Corsair);
    }

    void CycleEnemyCount(EnemyBotKind kind)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return;

        int[] options = kind == EnemyBotKind.SpaceMine ? SpaceMineCountOptions : EnemyCountOptions;
        CycleIntSetting(definition.CountRoomKey, options, RoomSettings.GetEnemyCount(kind), definition.DefaultCount);
    }

    int[] GetEnemyHpOptions(EnemyBotKind kind)
    {
        return kind == EnemyBotKind.PirateBase ? HeavyEnemyHpOptions : EnemyHpOptions;
    }

    int[] GetEnemyShieldOptions(EnemyBotKind kind)
    {
        return kind == EnemyBotKind.PirateBase ? HeavyEnemyShieldOptions : EnemyShieldOptions;
    }

    void CycleEnemyHp(EnemyBotKind kind)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return;

        int nextValue = GetNextOptionValue(GetEnemyHpOptions(kind), RoomSettings.GetEnemyHp(kind), definition.DefaultHp);
        Hashtable props = new Hashtable();
        props[definition.HpRoomKey] = nextValue;
        if (kind == EnemyBotKind.Corsair)
            props[RoomSettings.CorsairHpKey] = nextValue;

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleEnemyShield(EnemyBotKind kind)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return;

        CycleIntSetting(definition.ShieldRoomKey, GetEnemyShieldOptions(kind), RoomSettings.GetEnemyShield(kind), definition.DefaultShield);
    }

    void CycleEnemyDamage(EnemyBotKind kind)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return;

        CycleIntSetting(definition.DamageRoomKey, EnemyDamageOptions, RoomSettings.GetEnemyBaseDamage(kind), definition.DefaultDamage);
    }

    void CycleEnemySpeed(EnemyBotKind kind)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return;

        float nextValue = GetNextOptionValue(EnemySpeedOptions, RoomSettings.GetEnemySpeedMultiplier(kind), definition.DefaultSpeedMultiplier);
        Hashtable props = new Hashtable();
        props[definition.SpeedRoomKey] = nextValue;
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleEnemySpawnSecond(EnemyBotKind kind)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return;

        int nextValue = GetNextOptionValue(EnemySpawnSecondOptions, RoomSettings.GetEnemySpawnSecond(kind), definition.DefaultSpawnSecond);
        Hashtable props = new Hashtable();
        props[definition.SpawnSecondRoomKey] = nextValue;
        if (kind == EnemyBotKind.Corsair)
            props[RoomSettings.CorsairSpawnSecondKey] = nextValue;

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleEnemyRespawnEnabled(EnemyBotKind kind)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return;

        Hashtable props = new Hashtable();
        props[definition.RespawnEnabledRoomKey] = !RoomSettings.GetEnemyRespawnEnabled(kind);
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleEnemyRespawnInterval(EnemyBotKind kind)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return;

        CycleIntSetting(
            definition.RespawnIntervalRoomKey,
            EnemyRespawnIntervalOptions,
            RoomSettings.GetEnemyRespawnIntervalSeconds(kind),
            RoomSettings.DefaultEnemyRespawnIntervalSeconds);
    }

    void CycleObstacleWeightFactor()
    {
        CycleIntSetting(RoomSettings.ObstacleWeightFactorKey, ObstacleWeightFactorOptions, GetObstacleWeightFactor(), RoomSettings.DefaultObstacleWeightFactor);
    }

    void CycleTreasureWeightFactor()
    {
        CycleIntSetting(RoomSettings.TreasureWeightFactorKey, TreasureWeightFactorOptions, GetTreasureWeightFactor(), RoomSettings.DefaultTreasureWeightFactor);
    }

    void CycleBulletPushMultiplier()
    {
        CycleIntSetting(
            RoomSettings.BulletPushMultiplierKey,
            BulletPushMultiplierOptions,
            GetBulletPushMultiplier(),
            RoomSettings.DefaultBulletPushMultiplier);
    }

    void CycleBatteringDamage()
    {
        CycleIntSetting(
            RoomSettings.BatteringDamageKey,
            BatteringDamageOptions,
            GetBatteringDamage(),
            RoomSettings.DefaultBatteringDamage);
    }

    void CycleEnemyDamageMultiplier()
    {
        CycleIntSetting(
            RoomSettings.EnemyDamageMultiplierPercentKey,
            EnemyBalancePercentOptions,
            RoomSettings.GetEnemyDamageMultiplierPercent(),
            RoomSettings.DefaultEnemyDamageMultiplierPercent);
    }

    void CycleEnemyAttackWindupMultiplier()
    {
        CycleIntSetting(
            RoomSettings.EnemyAttackWindupMultiplierPercentKey,
            EnemyBalancePercentOptions,
            RoomSettings.GetEnemyAttackWindupMultiplierPercent(),
            RoomSettings.DefaultEnemyAttackWindupMultiplierPercent);
    }

    void CycleEnemyAttackCooldownMultiplier()
    {
        CycleIntSetting(
            RoomSettings.EnemyAttackCooldownMultiplierPercentKey,
            EnemyBalancePercentOptions,
            RoomSettings.GetEnemyAttackCooldownMultiplierPercent(),
            RoomSettings.DefaultEnemyAttackCooldownMultiplierPercent);
    }

    void OpenGunSetup()
    {
        if (!PhotonNetwork.InRoom || RoomSettings.GetSessionState() != RoomSettings.SessionStateInLobby)
            return;

        GunSetupOverlayUI.Show();
    }

    void CycleCollectKeepAliveRangeBonus()
    {
        CycleIntSetting(
            RoomSettings.CollectKeepAliveRangeBonusPercentKey,
            CollectKeepAliveRangeBonusPercentOptions,
            RoomSettings.GetCollectKeepAliveRangeBonusPercent(),
            RoomSettings.DefaultCollectKeepAliveRangeBonusPercent);
    }

    void CycleHapticsEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.HapticsEnabledKey] = !RoomSettings.AreHapticsEnabled();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleFpsCounterEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.FpsCounterEnabledKey] = !RoomSettings.IsFpsCounterEnabled();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleNeutralRidersEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.NeutralRidersEnabledKey] = !RoomSettings.AreNeutralRidersEnabled();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleNeutralRiderCount()
    {
        CycleIntSetting(
            RoomSettings.NeutralRidersCountKey,
            NeutralRiderCountOptions,
            RoomSettings.GetNeutralRiderCount(),
            RoomSettings.DefaultNeutralRidersCount);
    }

    void CycleNeutralRiderAggression()
    {
        CycleStringSetting(
            RoomSettings.NeutralRidersAggressionKey,
            NeutralRiderAggressionOptions,
            RoomSettings.GetNeutralRiderAggression(),
            RoomSettings.DefaultNeutralRiderAggression);
    }

    void CycleIntSetting(string key, int[] options, int current, int fallbackIndexValue)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[key] = GetNextOptionValue(options, current, fallbackIndexValue);
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleFloatSetting(string key, float[] options, float current, float fallbackIndexValue)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[key] = GetNextOptionValue(options, current, fallbackIndexValue);
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    int GetNextOptionValue(int[] options, int current, int fallbackIndexValue)
    {
        int index = System.Array.IndexOf(options, current);
        if (index < 0)
        {
            index = System.Array.IndexOf(options, fallbackIndexValue);
            if (index < 0)
                index = GetNearestOptionIndex(options, current);
        }

        int nextIndex = (index + 1) % options.Length;
        return options[nextIndex];
    }

    float GetNextOptionValue(float[] options, float current, float fallbackIndexValue)
    {
        if (options == null || options.Length == 0)
            return fallbackIndexValue;

        int index = -1;
        for (int i = 0; i < options.Length; i++)
        {
            if (Mathf.Abs(options[i] - current) < 0.01f)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            float bestDistance = float.MaxValue;
            for (int i = 0; i < options.Length; i++)
            {
                float distance = Mathf.Abs(options[i] - current);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    index = i;
                }
            }
        }

        int nextIndex = (Mathf.Max(0, index) + 1) % options.Length;
        return options[nextIndex];
    }

    int GetNearestOptionIndex(int[] options, int target)
    {
        if (options == null || options.Length == 0)
            return 0;

        int bestIndex = 0;
        int bestDistance = Mathf.Abs(options[0] - target);
        for (int i = 1; i < options.Length; i++)
        {
            int distance = Mathf.Abs(options[i] - target);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    void CycleDensitySetting(string key, string current)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        int index = System.Array.IndexOf(DensityOptions, current);
        if (index < 0)
            index = 0;

        int nextIndex = (index + 1) % DensityOptions.Length;

        Hashtable props = new Hashtable();
        props[key] = DensityOptions[nextIndex];
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleStringSetting(string key, string[] options, string current, string fallbackValue)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || options == null || options.Length == 0)
            return;

        int index = System.Array.IndexOf(options, current);
        if (index < 0)
        {
            index = System.Array.IndexOf(options, fallbackValue);
            if (index < 0)
                index = 0;
        }

        int nextIndex = (index + 1) % options.Length;

        Hashtable props = new Hashtable();
        props[key] = options[nextIndex];
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }
}
