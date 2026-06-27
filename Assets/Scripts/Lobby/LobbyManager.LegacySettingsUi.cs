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
    void EnsurePlayerStatusListExists()
    {
        if (playerStatusListText != null && playerStatusListText.gameObject.scene.IsValid())
            return;

        Transform existing = transform.Find("RoomPlayersText");
        if (existing != null)
        {
            playerStatusListText = existing.GetComponent<TMP_Text>();
            if (playerStatusListText != null)
                return;
        }

        GameObject textObject = new GameObject("RoomPlayersText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(transform, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -26f);
        rect.sizeDelta = new Vector2(390f, 145f);

        playerStatusListText = textObject.GetComponent<TextMeshProUGUI>();
        playerStatusListText.fontSize = 22f;
        playerStatusListText.fontStyle = FontStyles.Bold;
        playerStatusListText.alignment = TextAlignmentOptions.TopLeft;
        playerStatusListText.textWrappingMode = TextWrappingModes.NoWrap;
        playerStatusListText.color = new Color(0.94f, 0.97f, 1f, 1f);
        playerStatusListText.text = string.Empty;
    }

    void EnsureHostSettingsUiExists()
    {
        EnsureSettingsLayoutContainers();
        EnsureLobbyMapUiExists();

        roundSettingButton = EnsureSettingButton(ref roundSettingText, roundSettingButton, "RoundSettingButton", "RoundSettingText", Vector2.zero, CycleRoundDuration);
        mapSizeSettingButton = EnsureSettingButton(ref mapSizeSettingText, mapSizeSettingButton, "MapSizeSettingButton", "MapSizeSettingText", Vector2.zero, CycleMapSize);
        toxicBordersSettingButton = EnsureSettingButton(ref toxicBordersSettingText, toxicBordersSettingButton, "ToxicBordersSettingButton", "ToxicBordersSettingText", Vector2.zero, CycleToxicBordersEnabled);
        mapBackgroundSettingButton = EnsureSettingButton(ref mapBackgroundSettingText, mapBackgroundSettingButton, "MapBackgroundSettingButton", "MapBackgroundSettingText", Vector2.zero, CycleMapBackground);
        visualEffectsSettingButton = EnsureSettingButton(ref visualEffectsSettingText, visualEffectsSettingButton, "VisualEffectsSettingButton", "VisualEffectsSettingText", Vector2.zero, CycleVisualEffectsEnabled);
        advancedSpawnVfxSettingButton = EnsureSettingButton(ref advancedSpawnVfxSettingText, advancedSpawnVfxSettingButton, "AdvancedSpawnVfxSettingButton", "AdvancedSpawnVfxSettingText", Vector2.zero, CycleAdvancedSpawnVfxEnabled);
        lowHpHullSparksSettingButton = EnsureSettingButton(ref lowHpHullSparksSettingText, lowHpHullSparksSettingButton, "LowHpHullSparksSettingButton", "LowHpHullSparksSettingText", Vector2.zero, CycleLowHpHullSparksEnabled);
        boomVfxSettingButton = EnsureSettingButton(ref boomVfxSettingText, boomVfxSettingButton, "BoomVfxSettingButton", "BoomVfxSettingText", Vector2.zero, CycleBoomVfxEnabled);
        dynamicCameraZoomSettingButton = EnsureSettingButton(ref dynamicCameraZoomSettingText, dynamicCameraZoomSettingButton, "DynamicCameraZoomSettingButton", "DynamicCameraZoomSettingText", Vector2.zero, CycleDynamicCameraZoomEnabled);
        HideDeprecatedSettingButton("AdvancedBackgroundSettingButton", "AdvancedBackgroundSettingText");
        parallaxBackgroundSettingButton = EnsureSettingButton(ref parallaxBackgroundSettingText, parallaxBackgroundSettingButton, "ParallaxBackgroundSettingButton", "ParallaxBackgroundSettingText", Vector2.zero, CycleParallaxBackground);
        backgroundObjectSettingButton = EnsureSettingButton(ref backgroundObjectSettingText, backgroundObjectSettingButton, "BackgroundObjectSettingButton", "BackgroundObjectSettingText", Vector2.zero, CycleBackgroundObject);
        gravityWellPhysicsSettingButton = EnsureSettingButton(ref gravityWellPhysicsSettingText, gravityWellPhysicsSettingButton, "GravityWellPhysicsSettingButton", "GravityWellPhysicsSettingText", Vector2.zero, CycleGravityWellPhysicsEnabled);
        HideDeprecatedSettingButton("StartingVfxSettingButton", "StartingVfxSettingText");
        endDisasterSettingButton = EnsureSettingButton(ref endDisasterSettingText, endDisasterSettingButton, "EndDisasterSettingButton", "EndDisasterSettingText", Vector2.zero, CycleEndDisasterMode);
        endDisasterTimeSettingButton = EnsureSettingButton(ref endDisasterTimeSettingText, endDisasterTimeSettingButton, "EndDisasterTimeSettingButton", "EndDisasterTimeSettingText", Vector2.zero, CycleEndDisasterWarningSeconds);
        obstacleSettingButton = EnsureSettingButton(ref obstacleSettingText, obstacleSettingButton, "ObstacleSettingButton", "ObstacleSettingText", Vector2.zero, CycleObstacleDensity);
        obstacleDestroySettingButton = EnsureSettingButton(ref obstacleDestroySettingText, obstacleDestroySettingButton, "ObstacleDestroySettingButton", "ObstacleDestroySettingText", Vector2.zero, CycleObstacleDestroyEnabled);
        obstacleHpValueSettingButton = EnsureSettingButton(ref obstacleHpValueSettingText, obstacleHpValueSettingButton, "ObstacleHpValueSettingButton", "ObstacleHpValueSettingText", Vector2.zero, CycleObstacleHp);
        obstacleSizeSettingButton = EnsureSettingButton(ref obstacleSizeSettingText, obstacleSizeSettingButton, "ObstacleSizeSettingButton", "ObstacleSizeSettingText", Vector2.zero, CycleObstacleSizePercent);
        obstacleNoBordersSettingButton = EnsureSettingButton(ref obstacleNoBordersSettingText, obstacleNoBordersSettingButton, "ObstacleNoBordersSettingButton", "ObstacleNoBordersSettingText", Vector2.zero, CycleObstacleNoBorders);
        treasureSettingButton = EnsureSettingButton(ref treasureSettingText, treasureSettingButton, "TreasureSettingButton", "TreasureSettingText", Vector2.zero, CycleTreasureDensity);
        radioactiveTreasureSettingButton = EnsureSettingButton(ref radioactiveTreasureSettingText, radioactiveTreasureSettingButton, "RadioactiveTreasureSettingButton", "RadioactiveTreasureSettingText", Vector2.zero, CycleRadioactiveTreasureDensity);
        alienSecretsSettingButton = EnsureSettingButton(ref alienSecretsSettingText, alienSecretsSettingButton, "AlienSecretsSettingButton", "AlienSecretsSettingText", Vector2.zero, CycleAlienSecretsDensity);
        resourceRichnessSettingButton = EnsureSettingButton(ref resourceRichnessSettingText, resourceRichnessSettingButton, "ResourceRichnessSettingButton", "ResourceRichnessSettingText", Vector2.zero, CycleResourceRichness);
        spaceJunkSettingButton = EnsureSettingButton(ref spaceJunkSettingText, spaceJunkSettingButton, "SpaceJunkSettingButton", "SpaceJunkSettingText", Vector2.zero, CycleSpaceJunkDensity);
        containersSettingButton = EnsureSettingButton(ref containersSettingText, containersSettingButton, "ContainersSettingButton", "ContainersSettingText", Vector2.zero, CycleContainersDensity);
        artifactAsteroidsSettingButton = EnsureSettingButton(ref artifactAsteroidsSettingText, artifactAsteroidsSettingButton, "ArtifactAsteroidsSettingButton", "ArtifactAsteroidsSettingText", Vector2.zero, CycleArtifactAsteroidsDensity);
        randomLootWreckSettingButton = EnsureSettingButton(ref randomLootWreckSettingText, randomLootWreckSettingButton, "RandomLootWreckSettingButton", "RandomLootWreckSettingText", Vector2.zero, CycleRandomLootWreckCount);
        hiddenTreasureSettingButton = EnsureSettingButton(ref hiddenTreasureSettingText, hiddenTreasureSettingButton, "HiddenTreasureSettingButton", "HiddenTreasureSettingText", Vector2.zero, CycleHiddenTreasureEnabled);
        nebulaSettingButton = EnsureSettingButton(ref nebulaSettingText, nebulaSettingButton, "NebulaSettingButton", "NebulaSettingText", Vector2.zero, CycleNebulaDensity);
        fireNebulaSettingButton = EnsureSettingButton(ref fireNebulaSettingText, fireNebulaSettingButton, "FireNebulaSettingButton", "FireNebulaSettingText", Vector2.zero, CycleFireNebulaDensity);
        toxicNebulaSettingButton = EnsureSettingButton(ref toxicNebulaSettingText, toxicNebulaSettingButton, "ToxicNebulaSettingButton", "ToxicNebulaSettingText", Vector2.zero, CycleToxicNebulaDensity);
        nebulaSizeSettingButton = EnsureSettingButton(ref nebulaSizeSettingText, nebulaSizeSettingButton, "NebulaSizeSettingButton", "NebulaSizeSettingText", Vector2.zero, CycleNebulaSize);
        fireNebulaSizeSettingButton = EnsureSettingButton(ref fireNebulaSizeSettingText, fireNebulaSizeSettingButton, "FireNebulaSizeSettingButton", "FireNebulaSizeSettingText", Vector2.zero, CycleFireNebulaSize);
        toxicNebulaSizeSettingButton = EnsureSettingButton(ref toxicNebulaSizeSettingText, toxicNebulaSizeSettingButton, "ToxicNebulaSizeSettingButton", "ToxicNebulaSizeSettingText", Vector2.zero, CycleToxicNebulaSize);
        advancedNebulaSettingButton = EnsureSettingButton(ref advancedNebulaSettingText, advancedNebulaSettingButton, "AdvancedNebulaSettingButton", "AdvancedNebulaSettingText", Vector2.zero, CycleAdvancedNebulaEnabled);
        cloudsSettingButton = EnsureSettingButton(ref cloudsSettingText, cloudsSettingButton, "CloudsSettingButton", "CloudsSettingText", Vector2.zero, CycleCloudsDensity);
        cloudsSizeSettingButton = EnsureSettingButton(ref cloudsSizeSettingText, cloudsSizeSettingButton, "CloudsSizeSettingButton", "CloudsSizeSettingText", Vector2.zero, CycleCloudsSize);
        extractionSettingButton = EnsureSettingButton(ref extractionSettingText, extractionSettingButton, "ExtractionSettingButton", "ExtractionSettingText", Vector2.zero, CycleExtractionCount);
        extractionTypeSettingButton = EnsureSettingButton(ref extractionTypeSettingText, extractionTypeSettingButton, "ExtractionTypeSettingButton", "ExtractionTypeSettingText", Vector2.zero, CycleExtractionType);
        repairBaySettingButton = EnsureSettingButton(ref repairBaySettingText, repairBaySettingButton, "RepairBaySettingButton", "RepairBaySettingText", Vector2.zero, CycleRepairBayCount);
        spaceFactorySettingButton = EnsureSettingButton(ref spaceFactorySettingText, spaceFactorySettingButton, "SpaceFactorySettingButton", "SpaceFactorySettingText", Vector2.zero, CycleSpaceFactoryCount);
        scienceStationSettingButton = EnsureSettingButton(ref scienceStationSettingText, scienceStationSettingButton, "ScienceStationSettingButton", "ScienceStationSettingText", Vector2.zero, CycleScienceStationCount);
        avengerPlotSettingButton = EnsureSettingButton(ref avengerPlotSettingText, avengerPlotSettingButton, "AvengerPlotSettingButton", "AvengerPlotSettingText", Vector2.zero, CycleAvengerPlotEnabled);
        viperPlotChanceSettingButton = EnsureSettingButton(ref viperPlotChanceSettingText, viperPlotChanceSettingButton, "ViperPlotChanceSettingButton", "ViperPlotChanceSettingText", Vector2.zero, CycleViperPlotChancePercent);
        arrowPlotChanceSettingButton = EnsureSettingButton(ref arrowPlotChanceSettingText, arrowPlotChanceSettingButton, "ArrowPlotChanceSettingButton", "ArrowPlotChanceSettingText", Vector2.zero, CycleArrowPlotChancePercent);
        bisonPlotChanceSettingButton = EnsureSettingButton(ref bisonPlotChanceSettingText, bisonPlotChanceSettingButton, "BisonPlotChanceSettingButton", "BisonPlotChanceSettingText", Vector2.zero, CycleBisonPlotChancePercent);
        invaderPlotChanceSettingButton = EnsureSettingButton(ref invaderPlotChanceSettingText, invaderPlotChanceSettingButton, "InvaderPlotChanceSettingButton", "InvaderPlotChanceSettingText", Vector2.zero, CycleInvaderPlotChancePercent);
        boosterSettingButton = EnsureSettingButton(ref boosterSettingText, boosterSettingButton, "BoosterSettingButton", "BoosterSettingText", Vector2.zero, CycleBoosterSlowdown);
        HideDeprecatedSettingButton("AmmoSettingButton", "AmmoSettingText");
        boosterDelaySettingButton = EnsureSettingButton(ref boosterDelaySettingText, boosterDelaySettingButton, "BoosterDelaySettingButton", "BoosterDelaySettingText", Vector2.zero, CycleBoosterRecoveryDelay);
        advancedBoosterSettingButton = EnsureSettingButton(ref advancedBoosterSettingText, advancedBoosterSettingButton, "AdvancedBoosterSettingButton", "AdvancedBoosterSettingText", Vector2.zero, CycleAdvancedBoosterEnabled);
        HideDeprecatedSettingButton("MaxInputBoostSettingButton", "MaxInputBoostSettingText");
        shipDriftSettingButton = EnsureSettingButton(ref shipDriftSettingText, shipDriftSettingButton, "ShipDriftSettingButton", "ShipDriftSettingText", Vector2.zero, CycleShipDriftEnabled);
        deathTimerSettingButton = EnsureSettingButton(ref deathTimerSettingText, deathTimerSettingButton, "DeathTimerSettingButton", "DeathTimerSettingText", Vector2.zero, CycleLastShipTimerMultiplier);
        inventoryLossSettingButton = EnsureSettingButton(ref inventoryLossSettingText, inventoryLossSettingButton, "InventoryLossSettingButton", "InventoryLossSettingText", Vector2.zero, CycleInventoryLossEnabled);
        equipmentLossSettingButton = EnsureSettingButton(ref equipmentLossSettingText, equipmentLossSettingButton, "EquipmentLossSettingButton", "EquipmentLossSettingText", Vector2.zero, CycleEquipmentLossEnabled);
        cosmicWormSettingButton = EnsureSettingButton(ref cosmicWormSettingText, cosmicWormSettingButton, "CosmicWormSettingButton", "CosmicWormSettingText", Vector2.zero, CycleCosmicWormEffect);
        crazyEnemiesEffectSettingButton = EnsureSettingButton(ref crazyEnemiesEffectSettingText, crazyEnemiesEffectSettingButton, "CrazyEnemiesEffectSettingButton", "CrazyEnemiesEffectSettingText", Vector2.zero, CycleCrazyEnemiesEffect);
        fogOfWarEffectSettingButton = EnsureSettingButton(ref fogOfWarEffectSettingText, fogOfWarEffectSettingButton, "FogOfWarEffectSettingButton", "FogOfWarEffectSettingText", Vector2.zero, CycleFogOfWarEffect);
        pirateBaseEffectSettingButton = EnsureSettingButton(ref pirateBaseEffectSettingText, pirateBaseEffectSettingButton, "PirateBaseEffectSettingButton", "PirateBaseEffectSettingText", Vector2.zero, CyclePirateBaseEffect);
        asteroidShowerEffectSettingButton = EnsureSettingButton(ref asteroidShowerEffectSettingText, asteroidShowerEffectSettingButton, "AsteroidShowerEffectSettingButton", "AsteroidShowerEffectSettingText", Vector2.zero, CycleAsteroidShowerEffect);
        HideDeprecatedSettingButton("ShootingModelSettingButton", "ShootingModelSettingText");
        HideDeprecatedSettingButton("SuperAttackSettingButton", "SuperAttackSettingText");
        HideDeprecatedSettingButton("AdvancedMovingJoystickSettingButton", "AdvancedMovingJoystickSettingText");
        HideDeprecatedSettingButton("AdvancedShootingJoystickSettingButton", "AdvancedShootingJoystickSettingText");
        HideDeprecatedSettingButton("DynamicUseSettingButton", "DynamicUseSettingText");
        collectKeepAliveRangeBonusSettingButton = EnsureSettingButton(ref collectKeepAliveRangeBonusSettingText, collectKeepAliveRangeBonusSettingButton, "CollectKeepAliveRangeBonusSettingButton", "CollectKeepAliveRangeBonusSettingText", Vector2.zero, CycleCollectKeepAliveRangeBonus);
        hapticsSettingButton = EnsureSettingButton(ref hapticsSettingText, hapticsSettingButton, "HapticsSettingButton", "HapticsSettingText", Vector2.zero, CycleHapticsEnabled);
        fpsCounterSettingButton = EnsureSettingButton(ref fpsCounterSettingText, fpsCounterSettingButton, "FpsCounterSettingButton", "FpsCounterSettingText", Vector2.zero, CycleFpsCounterEnabled);
        diagnosticsGcSettingButton = EnsureSettingButton(ref diagnosticsGcSettingText, diagnosticsGcSettingButton, "DiagnosticsGcSettingButton", "DiagnosticsGcSettingText", Vector2.zero, CycleDiagnosticsGcEnabled);
        diagnosticsSceneCountsSettingButton = EnsureSettingButton(ref diagnosticsSceneCountsSettingText, diagnosticsSceneCountsSettingButton, "DiagnosticsSceneCountsSettingButton", "DiagnosticsSceneCountsSettingText", Vector2.zero, CycleDiagnosticsSceneCountsEnabled);
        diagnosticsNetworkSettingButton = EnsureSettingButton(ref diagnosticsNetworkSettingText, diagnosticsNetworkSettingButton, "DiagnosticsNetworkSettingButton", "DiagnosticsNetworkSettingText", Vector2.zero, CycleDiagnosticsNetworkEnabled);
        neutralRidersEnabledSettingButton = EnsureSettingButton(ref neutralRidersEnabledSettingText, neutralRidersEnabledSettingButton, "NeutralRidersEnabledSettingButton", "NeutralRidersEnabledSettingText", Vector2.zero, CycleNeutralRidersEnabled);
        neutralRidersCountSettingButton = EnsureSettingButton(ref neutralRidersCountSettingText, neutralRidersCountSettingButton, "NeutralRidersCountSettingButton", "NeutralRidersCountSettingText", Vector2.zero, CycleNeutralRiderCount);
        neutralRidersAggressionSettingButton = EnsureSettingButton(ref neutralRidersAggressionSettingText, neutralRidersAggressionSettingButton, "NeutralRidersAggressionSettingButton", "NeutralRidersAggressionSettingText", Vector2.zero, CycleNeutralRiderAggression);
        gunSetupSettingButton = EnsureSettingButton(ref gunSetupSettingText, gunSetupSettingButton, "GunSetupSettingButton", "GunSetupSettingText", Vector2.zero, OpenGunSetup);
        movingObjectsSettingButton = EnsureSettingButton(ref movingObjectsSettingText, movingObjectsSettingButton, "MovingObjectsSettingButton", "MovingObjectsSettingText", Vector2.zero, CycleMovingObjectsEnabled);
        bulletPushSettingButton = EnsureSettingButton(ref bulletPushSettingText, bulletPushSettingButton, "BulletPushSettingButton", "BulletPushSettingText", Vector2.zero, CycleBulletPushMultiplier);
        obstacleWeightSettingButton = EnsureSettingButton(ref obstacleWeightSettingText, obstacleWeightSettingButton, "ObstacleWeightSettingButton", "ObstacleWeightSettingText", Vector2.zero, CycleObstacleWeightFactor);
        treasureWeightSettingButton = EnsureSettingButton(ref treasureWeightSettingText, treasureWeightSettingButton, "TreasureWeightSettingButton", "TreasureWeightSettingText", Vector2.zero, CycleTreasureWeightFactor);
        batteringSettingButton = EnsureSettingButton(ref batteringSettingText, batteringSettingButton, "BatteringSettingButton", "BatteringSettingText", Vector2.zero, CycleBatteringDamage);
        enemyDamageMultiplierSettingButton = EnsureSettingButton(ref enemyDamageMultiplierSettingText, enemyDamageMultiplierSettingButton, "EnemyDamageMultiplierSettingButton", "EnemyDamageMultiplierSettingText", Vector2.zero, CycleEnemyDamageMultiplier);
        enemyAttackWindupMultiplierSettingButton = EnsureSettingButton(ref enemyAttackWindupMultiplierSettingText, enemyAttackWindupMultiplierSettingButton, "EnemyAttackWindupMultiplierSettingButton", "EnemyAttackWindupMultiplierSettingText", Vector2.zero, CycleEnemyAttackWindupMultiplier);
        enemyAttackCooldownMultiplierSettingButton = EnsureSettingButton(ref enemyAttackCooldownMultiplierSettingText, enemyAttackCooldownMultiplierSettingButton, "EnemyAttackCooldownMultiplierSettingButton", "EnemyAttackCooldownMultiplierSettingText", Vector2.zero, CycleEnemyAttackCooldownMultiplier);

        AttachLeftSectionButton(roundSettingButton, "ROUND");
        AttachLeftSectionButton(mapSizeSettingButton, "ROUND");
        AttachLeftSectionButton(deathTimerSettingButton, "ROUND");
        AttachLeftSectionButton(inventoryLossSettingButton, "ROUND");
        AttachLeftSectionButton(equipmentLossSettingButton, "ROUND");

        AttachLeftSectionButton(cosmicWormSettingButton, "ROUND EVENTS");
        AttachLeftSectionButton(crazyEnemiesEffectSettingButton, "ROUND EVENTS");
        AttachLeftSectionButton(fogOfWarEffectSettingButton, "ROUND EVENTS");
        AttachLeftSectionButton(pirateBaseEffectSettingButton, "ROUND EVENTS");
        AttachLeftSectionButton(asteroidShowerEffectSettingButton, "ROUND EVENTS");
        AttachLeftSectionButton(endDisasterSettingButton, "ROUND EVENTS");
        AttachLeftSectionButton(endDisasterTimeSettingButton, "ROUND EVENTS");

        AttachLeftSectionButton(obstacleSettingButton, "OBSTACLES");
        AttachLeftSectionButton(obstacleDestroySettingButton, "OBSTACLES");
        AttachLeftSectionButton(obstacleHpValueSettingButton, "OBSTACLES");
        AttachLeftSectionButton(obstacleSizeSettingButton, "OBSTACLES");
        AttachLeftSectionButton(obstacleNoBordersSettingButton, "OBSTACLES");
        AttachLeftSectionButton(movingObjectsSettingButton, "OBSTACLES");
        AttachLeftSectionButton(gravityWellPhysicsSettingButton, "OBSTACLES");
        AttachLeftSectionButton(obstacleWeightSettingButton, "OBSTACLES");

        AttachLeftSectionButton(treasureSettingButton, "LOOT & RESOURCES");
        AttachLeftSectionButton(radioactiveTreasureSettingButton, "LOOT & RESOURCES");
        AttachLeftSectionButton(resourceRichnessSettingButton, "LOOT & RESOURCES");
        AttachLeftSectionButton(spaceJunkSettingButton, "LOOT & RESOURCES");
        AttachLeftSectionButton(containersSettingButton, "LOOT & RESOURCES");
        AttachLeftSectionButton(artifactAsteroidsSettingButton, "LOOT & RESOURCES");
        AttachLeftSectionButton(randomLootWreckSettingButton, "LOOT & RESOURCES");
        AttachLeftSectionButton(hiddenTreasureSettingButton, "LOOT & RESOURCES");
        AttachLeftSectionButton(treasureWeightSettingButton, "LOOT & RESOURCES");

        AttachLeftSectionButton(toxicBordersSettingButton, "MAP HAZARDS");
        AttachLeftSectionButton(nebulaSettingButton, "MAP HAZARDS");
        AttachLeftSectionButton(fireNebulaSettingButton, "MAP HAZARDS");
        AttachLeftSectionButton(toxicNebulaSettingButton, "MAP HAZARDS");
        AttachLeftSectionButton(nebulaSizeSettingButton, "MAP HAZARDS");
        AttachLeftSectionButton(fireNebulaSizeSettingButton, "MAP HAZARDS");
        AttachLeftSectionButton(toxicNebulaSizeSettingButton, "MAP HAZARDS");
        AttachLeftSectionButton(advancedNebulaSettingButton, "MAP HAZARDS");
        AttachLeftSectionButton(cloudsSettingButton, "MAP HAZARDS");
        AttachLeftSectionButton(cloudsSizeSettingButton, "MAP HAZARDS");

        AttachLeftSectionButton(extractionSettingButton, "MAP OBJECTS");
        AttachLeftSectionButton(extractionTypeSettingButton, "MAP OBJECTS");
        AttachLeftSectionButton(repairBaySettingButton, "MAP OBJECTS");
        AttachLeftSectionButton(spaceFactorySettingButton, "MAP OBJECTS");
        AttachLeftSectionButton(scienceStationSettingButton, "MAP OBJECTS");

        AttachLeftSectionButton(avengerPlotSettingButton, "MAP PLOTS");
        AttachLeftSectionButton(viperPlotChanceSettingButton, "MAP PLOTS");
        AttachLeftSectionButton(arrowPlotChanceSettingButton, "MAP PLOTS");
        AttachLeftSectionButton(bisonPlotChanceSettingButton, "MAP PLOTS");
        AttachLeftSectionButton(invaderPlotChanceSettingButton, "MAP PLOTS");

        AttachLeftSectionButton(mapBackgroundSettingButton, "VISUALS");
        AttachLeftSectionButton(visualEffectsSettingButton, "VISUALS");
        AttachLeftSectionButton(advancedSpawnVfxSettingButton, "VISUALS");
        AttachLeftSectionButton(lowHpHullSparksSettingButton, "VISUALS");
        AttachLeftSectionButton(boomVfxSettingButton, "VISUALS");
        AttachLeftSectionButton(dynamicCameraZoomSettingButton, "VISUALS");
        AttachLeftSectionButton(parallaxBackgroundSettingButton, "VISUALS");
        AttachLeftSectionButton(backgroundObjectSettingButton, "VISUALS");

        AttachLeftSectionButton(boosterSettingButton, "SHIP FEEL");
        AttachLeftSectionButton(boosterDelaySettingButton, "SHIP FEEL");
        AttachLeftSectionButton(advancedBoosterSettingButton, "SHIP FEEL");
        AttachLeftSectionButton(shipDriftSettingButton, "SHIP FEEL");
        AttachLeftSectionButton(hapticsSettingButton, "SHIP FEEL");

        AttachLeftSectionButton(bulletPushSettingButton, "COMBAT FEEL");
        AttachLeftSectionButton(batteringSettingButton, "COMBAT FEEL");
        AttachLeftSectionButton(enemyDamageMultiplierSettingButton, "COMBAT FEEL");
        AttachLeftSectionButton(enemyAttackWindupMultiplierSettingButton, "COMBAT FEEL");
        AttachLeftSectionButton(enemyAttackCooldownMultiplierSettingButton, "COMBAT FEEL");
        AttachLeftSectionButton(collectKeepAliveRangeBonusSettingButton, "COMBAT FEEL");

        AttachLeftSectionButton(neutralRidersEnabledSettingButton, "NEUTRAL RIDERS");
        AttachLeftSectionButton(neutralRidersCountSettingButton, "NEUTRAL RIDERS");
        AttachLeftSectionButton(neutralRidersAggressionSettingButton, "NEUTRAL RIDERS");

        AttachLeftSectionButton(fpsCounterSettingButton, "DIAGNOSTICS");
        AttachLeftSectionButton(diagnosticsGcSettingButton, "DIAGNOSTICS");
        AttachLeftSectionButton(diagnosticsSceneCountsSettingButton, "DIAGNOSTICS");
        AttachLeftSectionButton(diagnosticsNetworkSettingButton, "DIAGNOSTICS");

        LayoutLeftSectionButtons();
        EnsureEnemySettingsUiExists();
        EnsureMapEffectChanceSettingsUiExists();
    }

    void EnsureLobbyNavigationUiExists()
    {
        backToRoundsButton = EnsureSettingButton(
            ref backToRoundsText,
            backToRoundsButton,
            "BackToRoundsButton",
            "BackToRoundsText",
            new Vector2(BottomActionBackX, BottomActionButtonsY),
            OnBackToRoundsClicked);

        if (backToRoundsButton != null)
        {
            RectTransform rect = backToRoundsButton.GetComponent<RectTransform>();
            if (rect != null)
                rect.sizeDelta = new Vector2(BottomActionButtonWidth, BottomActionButtonHeight);
        }

        EnsureBottomActionButtonsLayout();
    }

    void EnsureSettingsLayoutContainers()
    {
        Transform desiredParent = developerSettingsRootObject != null ? developerSettingsRootObject.transform : transform;

        if (leftSettingsViewportRect != null && leftSettingsViewportRect.gameObject.scene.IsValid())
        {
            if (leftSettingsViewportRect.transform.parent != desiredParent)
                leftSettingsViewportRect.transform.SetParent(desiredParent, false);
            ApplyLeftSettingsViewportLayout();
            return;
        }

        GameObject viewportObject = FindOrCreateChild(developerSettingsRootObject != null ? developerSettingsRootObject : gameObject, "LobbySettingsViewport", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        leftSettingsViewportRect = viewportObject.GetComponent<RectTransform>();
        leftSettingsViewportRect.anchorMin = new Vector2(0.5f, 1f);
        leftSettingsViewportRect.anchorMax = new Vector2(0.5f, 1f);
        leftSettingsViewportRect.pivot = new Vector2(0.5f, 1f);
        ApplyLeftSettingsViewportLayout();

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0.06f, 0.09f, 0.13f, 0.72f);
        viewportImage.raycastTarget = true;

        Mask mask = viewportObject.GetComponent<Mask>();
        mask.showMaskGraphic = true;

        leftSettingsScrollRect = viewportObject.GetComponent<ScrollRect>();
        leftSettingsScrollRect.horizontal = false;
        leftSettingsScrollRect.vertical = true;
        leftSettingsScrollRect.movementType = ScrollRect.MovementType.Clamped;
        leftSettingsScrollRect.scrollSensitivity = 36f;

        GameObject contentObject = FindOrCreateChild(viewportObject, "LobbySettingsContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        leftSettingsContentRect = contentObject.GetComponent<RectTransform>();
        leftSettingsContentRect.anchorMin = new Vector2(0f, 1f);
        leftSettingsContentRect.anchorMax = new Vector2(1f, 1f);
        leftSettingsContentRect.pivot = new Vector2(0.5f, 1f);
        leftSettingsContentRect.anchoredPosition = Vector2.zero;
        leftSettingsContentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup contentLayout = contentObject.GetComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(18, 18, 18, 170);
        contentLayout.spacing = 22f;
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        leftSettingsScrollRect.viewport = leftSettingsViewportRect;
        leftSettingsScrollRect.content = leftSettingsContentRect;

        EnsureLeftSectionContainer("ROUND");
        EnsureLeftSectionContainer("ROUND EVENTS");
        EnsureLeftSectionContainer("OBSTACLES");
        EnsureLeftSectionContainer("LOOT & RESOURCES");
        EnsureLeftSectionContainer("MAP HAZARDS");
        EnsureLeftSectionContainer("MAP OBJECTS");
        EnsureLeftSectionContainer("MAP PLOTS");
        EnsureLeftSectionContainer("VISUALS");
        EnsureLeftSectionContainer("SHIP FEEL");
        EnsureLeftSectionContainer("COMBAT FEEL");
        EnsureLeftSectionContainer("NEUTRAL RIDERS");
        EnsureLeftSectionContainer("DIAGNOSTICS");
        RemoveLeftSectionContainer("MAP RULES");
    }

    void ApplyLeftSettingsViewportLayout()
    {
        if (leftSettingsViewportRect == null)
            return;

        leftSettingsViewportRect.anchoredPosition = new Vector2(LeftColumnX, LeftColumnTopY);
        leftSettingsViewportRect.sizeDelta = new Vector2(LeftViewportWidth, LeftViewportHeight);
    }

    RectTransform EnsureLeftSectionContainer(string sectionName)
    {
        if (leftSectionContainers.TryGetValue(sectionName, out RectTransform existing) && existing != null && existing.gameObject.scene.IsValid())
            return existing;

        string safeName = sectionName.Replace(" ", string.Empty);
        GameObject sectionObject = new GameObject("LobbySection_" + safeName, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        sectionObject.transform.SetParent(leftSettingsContentRect, false);

        RectTransform rect = sectionObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = sectionObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = sectionObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        LayoutElement layoutElement = sectionObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = -1f;

        GameObject headerObject = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        headerObject.transform.SetParent(sectionObject.transform, false);
        RectTransform headerRect = headerObject.GetComponent<RectTransform>();
        headerRect.sizeDelta = new Vector2(0f, 34f);
        LayoutElement headerLayout = headerObject.GetComponent<LayoutElement>();
        headerLayout.preferredHeight = 34f;

        TMP_Text headerText = headerObject.GetComponent<TextMeshProUGUI>();
        headerText.text = sectionName;
        headerText.fontSize = 22f;
        headerText.fontStyle = FontStyles.Bold;
        headerText.alignment = TextAlignmentOptions.Left;
        headerText.color = new Color(0.86f, 0.95f, 1f, 0.96f);
        headerText.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_Text reference = FindAnyObjectByType<TMP_Text>();
        if (reference != null)
        {
            headerText.font = reference.font;
            headerText.fontSharedMaterial = reference.fontSharedMaterial;
        }

        leftSectionContainers[sectionName] = rect;
        return rect;
    }

    void RemoveLeftSectionContainer(string sectionName)
    {
        if (string.IsNullOrWhiteSpace(sectionName))
            return;

        if (leftSectionContainers.TryGetValue(sectionName, out RectTransform existing))
        {
            leftSectionContainers.Remove(sectionName);
            if (existing != null)
                Destroy(existing.gameObject);
        }

        if (leftSettingsContentRect == null)
            return;

        string safeName = sectionName.Replace(" ", string.Empty);
        Transform stale = leftSettingsContentRect.Find("LobbySection_" + safeName);
        if (stale != null)
            Destroy(stale.gameObject);
    }

    void AttachLeftSectionButton(Button button, string sectionName)
    {
        if (button == null)
            return;

        RectTransform sectionRect = EnsureLeftSectionContainer(sectionName);
        if (button.transform.parent != sectionRect)
            button.transform.SetParent(sectionRect, false);

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        if (buttonRect != null)
        {
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(1f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            buttonRect.anchoredPosition = Vector2.zero;
            buttonRect.sizeDelta = new Vector2(0f, 64f);
        }

        LayoutElement layout = button.GetComponent<LayoutElement>();
        if (layout == null)
            layout = button.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 64f;
        layout.flexibleWidth = 1f;
    }

    void LayoutLeftSectionButtons()
    {
        Canvas.ForceUpdateCanvases();
        if (leftSettingsScrollRect != null && !leftSettingsScrollInitialized)
        {
            leftSettingsScrollRect.verticalNormalizedPosition = 1f;
            leftSettingsScrollInitialized = true;
        }
    }

    TMP_Text CreateStandaloneLabel(Transform parent, string name, string value, Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.color = new Color(0.94f, 0.97f, 1f, 1f);
        text.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_Text reference = FindAnyObjectByType<TMP_Text>();
        if (reference != null)
        {
            text.font = reference.font;
            text.fontSharedMaterial = reference.fontSharedMaterial;
        }

        return text;
    }

    GameObject FindOrCreateChild(GameObject parent, string childName, params System.Type[] components)
    {
        Transform existing = parent.transform.Find(childName);
        if (existing != null)
            return existing.gameObject;

        GameObject child = new GameObject(childName, components);
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    void RefreshPlayerStatusList()
    {
        EnsurePlayerStatusListExists();

        if (playerStatusListText == null)
            return;

        if (!PhotonNetwork.InRoom)
        {
            playerStatusListText.text = "Joining room...";
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("PLAYERS");

        foreach (Player player in PhotonNetwork.PlayerList.OrderBy(p => p.ActorNumber))
        {
            bool ready = player.CustomProperties.TryGetValue("ready", out object readyValue) && readyValue is bool readyBool && readyBool;

            builder.Append(GetDisplayName(player));
            if (player == PhotonNetwork.LocalPlayer)
                builder.Append(" (YOU)");

            builder.Append("  -  ");
            builder.Append(ready ? "READY" : "NOT READY");
            builder.AppendLine();
        }

        playerStatusListText.text = builder.ToString().TrimEnd();
    }

    public void ForceRefreshUi()
    {
        RefreshPlayerStatusList();
        RefreshHostSettingsUi();
    }

    async Task RecordStartedGameAsync()
    {
        await PlayerProfileService.Instance.RecordGameStartedAsync();
    }
}
