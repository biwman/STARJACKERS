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
    void RefreshHostSettingsUi()
    {
        EnsureHostSettingsUiExists();
        EnsureLobbyNavigationUiExists();
        EnsureFullScreenLobbyUiExists();
        EnsureBottomActionButtonsLayout();

        bool isHost = PhotonNetwork.IsMasterClient;

        if (roundSettingText != null)
            roundSettingText.text = "ROUND TIME: " + FormatRoundDuration(GetRoundDuration());

        if (mapSizeSettingText != null)
            mapSizeSettingText.text = "MAP SIZE: " + FormatMapSize(GetMapSize());

        if (toxicBordersSettingText != null)
            toxicBordersSettingText.text = "TOXIC BORDERS: " + (RoomSettings.AreToxicBordersEnabled() ? "ON" : "OFF");

        if (mapBackgroundSettingText != null)
            mapBackgroundSettingText.text = "MAP BACKGROUND: " + FormatMapBackground(GetMapBackground());

        if (visualEffectsSettingText != null)
            visualEffectsSettingText.text = "VISUALS: " + (AreVisualEffectsEnabled() ? "ON" : "OFF");

        if (advancedSpawnVfxSettingText != null)
            advancedSpawnVfxSettingText.text = "ADVANCED SPAWN VFX: " + (RoomSettings.IsAdvancedSpawnVfxEnabled() ? "ON" : "OFF");

        if (lowHpHullSparksSettingText != null)
            lowHpHullSparksSettingText.text = "DAMAGE SPARKS: " + (RoomSettings.AreLowHpHullSparksEnabled() ? "ON" : "OFF");

        if (boomVfxSettingText != null)
            boomVfxSettingText.text = "BOOM VFX: " + (RoomSettings.AreBoomVfxEnabled() ? "ON" : "OFF");

        if (dynamicCameraZoomSettingText != null)
            dynamicCameraZoomSettingText.text = "DYNAMIC CAMERA ZOOM: " + (IsDynamicCameraZoomEnabled() ? "ON" : "OFF");

        if (parallaxBackgroundSettingText != null)
            parallaxBackgroundSettingText.text = "PARALLAX BACKGROUND: " + FormatParallaxBackground(GetParallaxBackground());

        if (backgroundObjectSettingText != null)
            backgroundObjectSettingText.text = "BACKGROUND OBJECT: " + FormatBackgroundObject(GetBackgroundObject());

        if (gravityWellPhysicsSettingText != null)
            gravityWellPhysicsSettingText.text = "GRAVITY FIELD: " + (RoomSettings.IsGravityWellPhysicsEnabled() ? "ON" : "OFF");

        if (endDisasterSettingText != null)
            endDisasterSettingText.text = "END DISASTER: " + FormatEndDisasterMode(GetEndDisasterMode());

        if (endDisasterTimeSettingText != null)
            endDisasterTimeSettingText.text = "END DISASTER TIME: " + GetEndDisasterWarningSeconds() + "s";

        if (obstacleSettingText != null)
            obstacleSettingText.text = "OBSTACLES DENSITY: " + FormatDensity(GetObstacleDensity());

        if (obstacleDestroySettingText != null)
            obstacleDestroySettingText.text = "OBSTACLES DESTROY: " + (AreObstaclesDestructible() ? "ON" : "OFF");

        if (obstacleHpValueSettingText != null)
            obstacleHpValueSettingText.text = "OBSTACLE HP: " + GetObstacleHp();

        if (obstacleSizeSettingText != null)
            obstacleSizeSettingText.text = "OBSTACLE SIZE: " + GetObstacleSizePercent() + "%";

        if (obstacleNoBordersSettingText != null)
            obstacleNoBordersSettingText.text = "OBSTACLES NO BORDERS: " + (AreObstaclesBorderless() ? "YES" : "NO");

        if (treasureSettingText != null)
            treasureSettingText.text = "RESOURCES DENSITY: " + FormatDensity(GetTreasureDensity());

        if (radioactiveTreasureSettingText != null)
            radioactiveTreasureSettingText.text = "RADIOACTIVE TREASURE: " + FormatRadioactiveTreasureDensity(GetRadioactiveTreasureDensity());

        if (alienSecretsSettingText != null)
            alienSecretsSettingText.text = "ALIEN SECRETS: " + FormatAlienSecretsDensity(GetAlienSecretsDensity());

        if (resourceRichnessSettingText != null)
            resourceRichnessSettingText.text = "RESOURCES RICHNESS: " + FormatResourceRichness(GetResourceRichness());

        if (spaceJunkSettingText != null)
            spaceJunkSettingText.text = "SPACE JUNK: " + FormatSpaceJunkDensity(GetSpaceJunkDensity());

        if (containersSettingText != null)
            containersSettingText.text = "CONTAINERS DENSITY: " + FormatContainersDensity(GetContainersDensity());

        if (artifactAsteroidsSettingText != null)
            artifactAsteroidsSettingText.text = "ARTIFACT ASTEROIDS: " + FormatArtifactAsteroidsDensity(GetArtifactAsteroidsDensity());

        if (randomLootWreckSettingText != null)
            randomLootWreckSettingText.text = "RANDOM WRECKS: " + FormatRandomLootWreckCount(GetRandomLootWreckCount());

        if (hiddenTreasureSettingText != null)
            hiddenTreasureSettingText.text = "HIDDEN TREASURE: " + (RoomSettings.IsHiddenTreasureEnabled() ? "ON" : "OFF");

        if (nebulaSettingText != null)
            nebulaSettingText.text = "NEBULA DENSITY: " + FormatDensity(GetNebulaDensity());

        if (fireNebulaSettingText != null)
            fireNebulaSettingText.text = "FIRE NEBULA DENSITY: " + FormatDensity(GetFireNebulaDensity());

        if (toxicNebulaSettingText != null)
            toxicNebulaSettingText.text = "TOXIC NEBULA DENSITY: " + FormatDensity(GetToxicNebulaDensity());

        if (nebulaSizeSettingText != null)
            nebulaSizeSettingText.text = "NEBULA SIZE: " + FormatNebulaSize(GetNebulaSize());

        if (fireNebulaSizeSettingText != null)
            fireNebulaSizeSettingText.text = "FIRE NEBULA SIZE: " + FormatNebulaSize(GetFireNebulaSize());

        if (toxicNebulaSizeSettingText != null)
            toxicNebulaSizeSettingText.text = "TOXIC NEBULA SIZE: " + FormatNebulaSize(GetToxicNebulaSize());

        if (advancedNebulaSettingText != null)
            advancedNebulaSettingText.text = "ADVANCED NEBULA: " + (RoomSettings.IsAdvancedNebulaEnabled() ? "ON" : "OFF");

        if (cloudsSettingText != null)
            cloudsSettingText.text = "CLOUDS DENSITY: " + FormatDensity(GetCloudsDensity());

        if (cloudsSizeSettingText != null)
            cloudsSizeSettingText.text = "CLOUDS SIZE: " + FormatNebulaSize(GetCloudsSize());

        if (extractionSettingText != null)
            extractionSettingText.text = "EXTRACTION ZONES: " + GetExtractionCount();

        if (extractionTypeSettingText != null)
            extractionTypeSettingText.text = "EXTRACTION TYPE: " + FormatExtractionType(GetExtractionType());

        if (repairBaySettingText != null)
            repairBaySettingText.text = "REPAIR BAY: " + GetRepairBayCount();

        if (spaceFactorySettingText != null)
            spaceFactorySettingText.text = "SPACE FACTORY: " + GetSpaceFactoryCount();

        if (scienceStationSettingText != null)
            scienceStationSettingText.text = "SCIENCE STATION: " + (GetScienceStationCount() > 0 ? "ON" : "OFF");

        if (avengerPlotSettingText != null)
            avengerPlotSettingText.text = "AVENGER PLOT: " + (RoomSettings.IsAvengerPlotEnabled() ? "ON" : "OFF");

        if (viperPlotChanceSettingText != null)
            viperPlotChanceSettingText.text = "VIPER PLOT CHANCE: " + RoomSettings.GetViperPlotChancePercent() + "%";

        if (arrowPlotChanceSettingText != null)
            arrowPlotChanceSettingText.text = "ARROW UNLOCK STORY: " + RoomSettings.GetArrowPlotChancePercent() + "%";

        if (bisonPlotChanceSettingText != null)
            bisonPlotChanceSettingText.text = "BISON PLOT CHANCE: " + RoomSettings.GetBisonPlotChancePercent() + "%";

        if (invaderPlotChanceSettingText != null)
            invaderPlotChanceSettingText.text = "INVADER PLOT CHANCE: " + RoomSettings.GetInvaderPlotChancePercent() + "%";

        if (boosterSettingText != null)
            boosterSettingText.text = "EMPTY BOOSTER SLOWDOWN: " + GetBoosterSlowdownPercent() + "%";

        if (boosterDelaySettingText != null)
            boosterDelaySettingText.text = "BOOST COOLDOWN: " + GetBoosterRecoveryDelay() + "s";

        if (advancedBoosterSettingText != null)
            advancedBoosterSettingText.text = "ADVANCED BOOSTER: " + (RoomSettings.IsAdvancedBoosterEnabled() ? "ON" : "OFF");

        if (shipDriftSettingText != null)
            shipDriftSettingText.text = "BRAKING DRIFT: PER SHIP";

        if (deathTimerSettingText != null)
            deathTimerSettingText.text = "LONE SHIP TIMER: " + FormatLastShipTimerMultiplier(GetLastShipTimerMultiplier());

        if (inventoryLossSettingText != null)
            inventoryLossSettingText.text = "INVENTORY LOSS: " + (RoomSettings.IsInventoryLossEnabled() ? "YES" : "NO");

        if (equipmentLossSettingText != null)
            equipmentLossSettingText.text = "EQUIPMENT LOSS: " + (RoomSettings.IsEquipmentLossEnabled() ? "YES" : "NO");

        if (cosmicWormSettingText != null)
            cosmicWormSettingText.text = "COSMIC WORM: " + FormatMapEffectSetting(RoomSettings.CosmicWormModeKey, RoomSettings.CosmicWormStartUtcMsKey, RoomSettings.CosmicWormActiveKey) + " (+RICHNESS, WORM)";

        if (crazyEnemiesEffectSettingText != null)
            crazyEnemiesEffectSettingText.text = "CRAZY ENEMIES: " + FormatMapEffectSetting(RoomSettings.CrazyEnemiesModeKey, RoomSettings.CrazyEnemiesStartUtcMsKey, RoomSettings.CrazyEnemiesActiveKey) + " (+DENSITY)";

        if (fogOfWarEffectSettingText != null)
            fogOfWarEffectSettingText.text = "FOG OF WAR: " + FormatMapEffectSetting(RoomSettings.FogOfWarModeKey, RoomSettings.FogOfWarStartUtcMsKey, RoomSettings.FogOfWarActiveKey) + " (+RICHNESS)";

        if (pirateBaseEffectSettingText != null)
            pirateBaseEffectSettingText.text = "PIRATE BASE: " + FormatMapEffectSetting(RoomSettings.PirateBaseModeKey, RoomSettings.PirateBaseStartUtcMsKey, RoomSettings.PirateBaseActiveKey) + " (+RICHNESS, BASE)";

        if (asteroidShowerEffectSettingText != null)
            asteroidShowerEffectSettingText.text = "ASTEROID SHOWER: " + FormatMapEffectSetting(RoomSettings.AsteroidShowerModeKey, RoomSettings.AsteroidShowerStartUtcMsKey, RoomSettings.AsteroidShowerActiveKey) + " (+RICHNESS)";

        if (movingObjectsSettingText != null)
            movingObjectsSettingText.text = "MOVING OBJECTS: " + FormatMovingObjectsMode(GetMovingObjectsMode());

        if (bulletPushSettingText != null)
            bulletPushSettingText.text = "BULLET PUSH: X" + GetBulletPushMultiplier();

        if (batteringSettingText != null)
            batteringSettingText.text = "BATTERING: " + FormatBatteringDamage(GetBatteringDamage());

        if (enemyDamageMultiplierSettingText != null)
            enemyDamageMultiplierSettingText.text = "ENEMY DAMAGE: " + RoomSettings.GetEnemyDamageMultiplierPercent() + "%";

        if (enemyAttackWindupMultiplierSettingText != null)
            enemyAttackWindupMultiplierSettingText.text = "ENEMY WINDUP: " + RoomSettings.GetEnemyAttackWindupMultiplierPercent() + "%";

        if (enemyAttackCooldownMultiplierSettingText != null)
            enemyAttackCooldownMultiplierSettingText.text = "ENEMY COOLDOWN: " + RoomSettings.GetEnemyAttackCooldownMultiplierPercent() + "%";

        if (collectKeepAliveRangeBonusSettingText != null)
            collectKeepAliveRangeBonusSettingText.text = "COLLECT RANGE BUFFER: " + RoomSettings.GetCollectKeepAliveRangeBonusPercent() + "%";

        if (hapticsSettingText != null)
            hapticsSettingText.text = "HAPTICS: " + (RoomSettings.AreHapticsEnabled() ? "ON" : "OFF");

        if (pcTouchJoystickTestModeSettingText != null)
            pcTouchJoystickTestModeSettingText.text = "PC TOUCH JOYSTICKS: " + (DeveloperInputSettings.PcTouchJoystickTestModeEnabled ? "ON" : "OFF");

        if (fpsCounterSettingText != null)
            fpsCounterSettingText.text = "FPS METER: " + (RoomSettings.IsFpsCounterEnabled() ? "ON" : "OFF");

        if (diagnosticsGcSettingText != null)
            diagnosticsGcSettingText.text = "GC METER: " + (RoomSettings.IsDiagnosticsGcEnabled() ? "ON" : "OFF");

        if (diagnosticsSceneCountsSettingText != null)
            diagnosticsSceneCountsSettingText.text = "SCENE COUNTS: " + (RoomSettings.IsDiagnosticsSceneCountsEnabled() ? "ON" : "OFF");

        if (diagnosticsNetworkSettingText != null)
            diagnosticsNetworkSettingText.text = "NETWORK METER: " + (RoomSettings.IsDiagnosticsNetworkEnabled() ? "ON" : "OFF");

        if (neutralRidersEnabledSettingText != null)
            neutralRidersEnabledSettingText.text = "NEUTRAL RIDERS: " + (RoomSettings.AreNeutralRidersEnabled() ? "ON" : "OFF");

        if (neutralRidersCountSettingText != null)
            neutralRidersCountSettingText.text = "NEUTRAL RIDERS COUNT: " + RoomSettings.GetNeutralRiderCount();

        if (neutralRidersAggressionSettingText != null)
            neutralRidersAggressionSettingText.text = "NEUTRAL RIDERS AGGRESSION: " + FormatNeutralRiderAggression(RoomSettings.GetNeutralRiderAggression());

        if (gunSetupSettingText != null)
            gunSetupSettingText.text = "GUN SETUP";

        if (obstacleWeightSettingText != null)
            obstacleWeightSettingText.text = "OBSTACLE MASS: " + RoomSettings.GetMassLabel(GetObstacleWeightFactor());

        if (treasureWeightSettingText != null)
            treasureWeightSettingText.text = "TREASURE MASS: " + RoomSettings.GetMassLabel(GetTreasureWeightFactor());

        SetSettingButtonState(roundSettingButton, isHost);
        SetSettingButtonState(mapSizeSettingButton, isHost);
        SetSettingButtonState(toxicBordersSettingButton, isHost);
        SetSettingButtonState(mapBackgroundSettingButton, isHost);
        SetSettingButtonState(visualEffectsSettingButton, isHost);
        SetSettingButtonState(advancedSpawnVfxSettingButton, isHost);
        SetSettingButtonState(lowHpHullSparksSettingButton, isHost);
        SetSettingButtonState(boomVfxSettingButton, isHost);
        SetSettingButtonState(dynamicCameraZoomSettingButton, isHost);
        SetSettingButtonState(parallaxBackgroundSettingButton, isHost);
        SetSettingButtonState(gravityWellPhysicsSettingButton, isHost);
        SetSettingButtonState(endDisasterSettingButton, isHost);
        SetSettingButtonState(endDisasterTimeSettingButton, isHost);
        SetSettingButtonState(obstacleSettingButton, isHost);
        SetSettingButtonState(obstacleDestroySettingButton, isHost);
        SetSettingButtonState(obstacleHpValueSettingButton, isHost);
        SetSettingButtonState(obstacleSizeSettingButton, isHost);
        SetSettingButtonState(obstacleNoBordersSettingButton, isHost);
        SetSettingButtonState(treasureSettingButton, isHost);
        SetSettingButtonState(radioactiveTreasureSettingButton, isHost);
        SetSettingButtonState(resourceRichnessSettingButton, isHost);
        SetSettingButtonState(spaceJunkSettingButton, isHost);
        SetSettingButtonState(containersSettingButton, isHost);
        SetSettingButtonState(artifactAsteroidsSettingButton, isHost);
        SetSettingButtonState(randomLootWreckSettingButton, isHost);
        SetSettingButtonState(hiddenTreasureSettingButton, isHost);
        SetSettingButtonState(nebulaSettingButton, isHost);
        SetSettingButtonState(fireNebulaSettingButton, isHost);
        SetSettingButtonState(toxicNebulaSettingButton, isHost);
        SetSettingButtonState(nebulaSizeSettingButton, isHost);
        SetSettingButtonState(fireNebulaSizeSettingButton, isHost);
        SetSettingButtonState(toxicNebulaSizeSettingButton, isHost);
        SetSettingButtonState(advancedNebulaSettingButton, isHost);
        SetSettingButtonState(cloudsSettingButton, isHost);
        SetSettingButtonState(cloudsSizeSettingButton, isHost);
        SetSettingButtonState(extractionSettingButton, isHost);
        SetSettingButtonState(extractionTypeSettingButton, isHost);
        SetSettingButtonState(repairBaySettingButton, isHost);
        SetSettingButtonState(spaceFactorySettingButton, isHost);
        SetSettingButtonState(scienceStationSettingButton, isHost);
        SetSettingButtonState(avengerPlotSettingButton, isHost);
        SetSettingButtonState(viperPlotChanceSettingButton, isHost);
        SetSettingButtonState(arrowPlotChanceSettingButton, isHost);
        SetSettingButtonState(bisonPlotChanceSettingButton, isHost);
        SetSettingButtonState(invaderPlotChanceSettingButton, isHost);
        SetSettingButtonState(boosterSettingButton, isHost);
        SetSettingButtonState(boosterDelaySettingButton, isHost);
        SetSettingButtonState(advancedBoosterSettingButton, isHost);
        SetSettingButtonState(shipDriftSettingButton, false);
        SetSettingButtonState(deathTimerSettingButton, isHost);
        SetSettingButtonState(inventoryLossSettingButton, isHost);
        SetSettingButtonState(equipmentLossSettingButton, isHost);
        SetSettingButtonState(cosmicWormSettingButton, isHost);
        SetSettingButtonState(crazyEnemiesEffectSettingButton, isHost);
        SetSettingButtonState(fogOfWarEffectSettingButton, isHost);
        SetSettingButtonState(pirateBaseEffectSettingButton, isHost);
        SetSettingButtonState(asteroidShowerEffectSettingButton, isHost);
        SetSettingButtonState(movingObjectsSettingButton, isHost);
        SetSettingButtonState(bulletPushSettingButton, isHost);
        SetSettingButtonState(batteringSettingButton, isHost);
        SetSettingButtonState(enemyDamageMultiplierSettingButton, isHost);
        SetSettingButtonState(enemyAttackWindupMultiplierSettingButton, isHost);
        SetSettingButtonState(enemyAttackCooldownMultiplierSettingButton, isHost);
        SetSettingButtonState(hapticsSettingButton, isHost);
        SetSettingButtonState(pcTouchJoystickTestModeSettingButton, true);
        SetSettingButtonState(fpsCounterSettingButton, isHost);
        SetSettingButtonState(diagnosticsGcSettingButton, isHost);
        SetSettingButtonState(diagnosticsSceneCountsSettingButton, isHost);
        SetSettingButtonState(diagnosticsNetworkSettingButton, isHost);
        SetSettingButtonState(neutralRidersEnabledSettingButton, isHost);
        SetSettingButtonState(neutralRidersCountSettingButton, isHost);
        SetSettingButtonState(neutralRidersAggressionSettingButton, isHost);
        SetSettingButtonState(gunSetupSettingButton, isHost);
        if (developerGunSetupButton != null)
            developerGunSetupButton.interactable = isHost;
        SetSettingButtonState(obstacleWeightSettingButton, isHost);
        SetSettingButtonState(treasureWeightSettingButton, isHost);
        RefreshLobbyMapSelectionUi(isHost);
        RefreshLobbyNavigationButton();
        RefreshEnemySettingTexts(isHost);
        RefreshMapEffectChanceSettingTexts(isHost);
        RefreshLobbyTopBar();
        RefreshLobbyScreenContent();
        if (ShouldShowFullScreenLobby())
            LayoutFullScreenLobbyUi();
    }

    void RefreshLobbyNavigationButton()
    {
        if (backToRoundsButton == null)
            return;

        if (ShouldShowFullScreenLobby())
        {
            backToRoundsButton.gameObject.SetActive(false);
            return;
        }

        bool inLobbyState = PhotonNetwork.InRoom && RoomSettings.GetSessionState() == RoomSettings.SessionStateInLobby;
        backToRoundsButton.interactable = inLobbyState;
        backToRoundsButton.gameObject.SetActive(true);
        backToRoundsButton.transform.SetAsLastSibling();

        Image image = backToRoundsButton.GetComponent<Image>();
        if (image != null)
        {
            image.color = inLobbyState
                ? PhotonNetwork.IsMasterClient
                    ? new Color(0.58f, 0.18f, 0.18f, 0.98f)
                    : new Color(0.16f, 0.34f, 0.58f, 0.98f)
                : new Color(0.12f, 0.14f, 0.18f, 0.72f);
        }

        if (backToRoundsText != null)
        {
            backToRoundsText.text = PhotonNetwork.IsMasterClient ? "CLOSE LOBBY" : "BACK TO ROUNDS";
            backToRoundsText.fontSize = 30f;
            backToRoundsText.characterSpacing = 2.2f;
        }
    }

    void SetSettingButtonState(Button button, bool interactable)
    {
        if (button == null)
            return;

        button.interactable = interactable;

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = interactable
                ? new Color(0.16f, 0.2f, 0.27f, 0.95f)
                : new Color(0.12f, 0.14f, 0.18f, 0.72f);
        }
    }

    void RefreshEnemySettingTexts(bool isHost)
    {
        for (int i = 0; i < EnemyBotCatalog.AllDefinitions.Count; i++)
        {
            EnemyBotDefinition definition = EnemyBotCatalog.AllDefinitions[i];
            SetEnemySettingText(definition.Kind, "enabled", RoomSettings.GetEnemyEnabled(definition.Kind) ? "ON" : "OFF");
            SetEnemySettingText(definition.Kind, "count", RoomSettings.GetEnemyCount(definition.Kind).ToString());
            SetEnemySettingText(definition.Kind, "respawn", RoomSettings.GetEnemyRespawnEnabled(definition.Kind) ? "YES" : "NO");
            SetEnemySettingText(definition.Kind, "hp", RoomSettings.GetEnemyHp(definition.Kind).ToString());
            SetEnemySettingText(definition.Kind, "shield", RoomSettings.GetEnemyShield(definition.Kind).ToString());
            SetEnemySettingText(definition.Kind, "damage", RoomSettings.GetEnemyBaseDamage(definition.Kind).ToString());
            SetEnemySettingText(definition.Kind, "speed", FormatEnemySpeed(RoomSettings.GetEnemySpeedMultiplier(definition.Kind)));
            SetEnemySettingText(definition.Kind, "time", RoomSettings.GetEnemySpawnSecond(definition.Kind) + "s");
            SetEnemySettingText(definition.Kind, "respawnTime", RoomSettings.GetEnemyRespawnIntervalSeconds(definition.Kind) + "s");

            SetSettingButtonState(GetEnemySettingButton(definition.Kind, "enabled"), isHost);
            SetSettingButtonState(GetEnemySettingButton(definition.Kind, "count"), isHost);
            SetSettingButtonState(GetEnemySettingButton(definition.Kind, "respawn"), isHost);
            SetSettingButtonState(GetEnemySettingButton(definition.Kind, "hp"), isHost);
            SetSettingButtonState(GetEnemySettingButton(definition.Kind, "shield"), isHost);
            SetSettingButtonState(GetEnemySettingButton(definition.Kind, "damage"), isHost);
            SetSettingButtonState(GetEnemySettingButton(definition.Kind, "speed"), isHost);
            SetSettingButtonState(GetEnemySettingButton(definition.Kind, "time"), isHost);
            SetSettingButtonState(GetEnemySettingButton(definition.Kind, "respawnTime"), isHost);
        }
    }

    void RefreshMapEffectChanceSettingTexts(bool isHost)
    {
        IReadOnlyList<LobbyMapDefinition> maps = LobbyMapCatalog.AllMaps;
        for (int mapIndex = 0; mapIndex < maps.Count; mapIndex++)
        {
            LobbyMapDefinition map = maps[mapIndex];
            if (map == null)
                continue;

            for (int ruleIndex = 0; ruleIndex < MapEffectChanceRuleIds.Length; ruleIndex++)
            {
                string ruleId = MapEffectChanceRuleIds[ruleIndex];
                SetMapEffectChanceSettingText(map.Id, ruleId, FormatMapEffectChancePercent(RoomSettings.GetMapEffectChancePercent(map.Id, ruleId)));
                SetSettingButtonState(GetMapEffectChanceButton(map.Id, ruleId), isHost);
            }
        }
    }

    string FormatEnemySpeed(float value)
    {
        if (Mathf.Abs(value - 0.25f) < 0.01f)
            return "x0.25";

        if (Mathf.Abs(value - 0.5f) < 0.01f)
            return "x0.5";

        if (Mathf.Abs(value - 1.5f) < 0.01f)
            return "x1.5";

        if (Mathf.Abs(value - 2f) < 0.01f)
            return "x2";

        return "x1";
    }

    string FormatLastShipTimerMultiplier(float value)
    {
        if (Mathf.Abs(value - Mathf.Round(value)) < 0.01f)
            return "X" + Mathf.RoundToInt(value);

        return "X" + value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
    }

    string FormatMovingObjectsMode(string mode)
    {
        switch (mode)
        {
            case RoomSettings.MovingObjectsModeOff:
                return "OFF";
            case RoomSettings.MovingObjectsModeOnlyRotate:
                return "ONLY ROTATE";
            default:
                return "ON";
        }
    }

    string FormatNeutralRiderAggression(string aggression)
    {
        switch (RoomSettings.NormalizeNeutralRiderAggression(aggression))
        {
            case RoomSettings.NeutralRiderAggressionLow:
                return "LOW";
            case RoomSettings.NeutralRiderAggressionHigh:
                return "HIGH";
            default:
                return "MEDIUM";
        }
    }

    string FormatBatteringDamage(int damage)
    {
        return damage <= 0 ? "OFF" : damage.ToString();
    }

    string FormatEndDisasterMode(string mode)
    {
        return RoomSettings.NormalizeEndDisasterMode(mode) == RoomSettings.EndDisasterMeteor
            ? "METEOR"
            : "OFF";
    }

    void SetEnemySettingText(EnemyBotKind kind, string suffix, string text)
    {
        if (enemySettingTexts.TryGetValue(GetEnemySettingUiKey(kind, suffix), out TMP_Text textField) && textField != null)
            textField.text = text;
    }

    string FormatMapEffectChancePercent(int percent)
    {
        return RoomSettings.NormalizeMapEffectChancePercent(percent) + "%";
    }

    Button GetMapEffectChanceButton(string mapId, string ruleId)
    {
        mapEffectChanceButtons.TryGetValue(GetMapEffectChanceUiKey(mapId, ruleId), out Button button);
        return button;
    }

    void SetMapEffectChanceSettingText(string mapId, string ruleId, string text)
    {
        if (mapEffectChanceTexts.TryGetValue(GetMapEffectChanceUiKey(mapId, ruleId), out TMP_Text textField) && textField != null)
            textField.text = text;
    }
}
