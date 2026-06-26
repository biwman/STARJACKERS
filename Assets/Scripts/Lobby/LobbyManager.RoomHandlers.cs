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
    void OnBackToRoundsClicked()
    {
        if (!PhotonNetwork.InRoom || RoomSettings.GetSessionState() != RoomSettings.SessionStateInLobby)
            return;

        HideFullScreenLobbyFlow();
        HideMapSelectionOverlay();
        NetworkManager.ReturnToSessionBrowserFromLobby();
    }

    void OnExitLobbyClicked()
    {
        OnBackToRoundsClicked();
    }

    public override void OnLeftRoom()
    {
        HideFullScreenLobbyFlow();

        CanvasGroup cg = EnsureCanvasGroup();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    void OnDeveloperSettingsClicked()
    {
        previousMapScreenBeforeDeveloperSettings = currentScreen == LobbyScreen.MapDetails ? LobbyScreen.MapDetails : LobbyScreen.MapSelection;
        SwitchLobbyScreen(LobbyScreen.DeveloperSettings);
    }

    void OnDeveloperBackClicked()
    {
        if (currentScreen == LobbyScreen.MapDetails)
        {
            SwitchLobbyScreen(LobbyScreen.MapSelection);
            return;
        }

        SwitchLobbyScreen(previousMapScreenBeforeDeveloperSettings);
    }

    void OnDeveloperCheatClicked()
    {
        if (PlayerProfileService.Instance == null || developerCheatOverlayObject == null)
            return;

        RefreshDeveloperCheatOverlay(string.Empty);
        developerCheatOverlayObject.SetActive(true);
        EnsureDeveloperCheatLayering();
    }

    async void OnDeveloperCheatAddMoneyClicked()
    {
        if (PlayerProfileService.Instance == null || developerCheatOverlayObject == null)
            return;

        if (developerCheatAddMoneyButton != null)
            developerCheatAddMoneyButton.interactable = false;

        try
        {
            RefreshDeveloperCheatOverlay("Adding 5000 Astrons...", true);
            await PlayerProfileService.Instance.AddAstronsAsync(5000);
            RefreshLobbyTopBar();
            RefreshDeveloperCheatOverlay("Added 5000 Astrons.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Developer cheat add money failed: " + ex);
            RefreshDeveloperCheatOverlay("Could not add Astrons.");
        }
        finally
        {
            RefreshDeveloperCheatOverlay();
        }
    }

    async void OnDeveloperCheatAddXpClicked()
    {
        if (PlayerProfileService.Instance == null || developerCheatOverlayObject == null)
            return;

        try
        {
            RefreshDeveloperCheatOverlay("Adding 1000 XP...", true);
            await PlayerProfileService.Instance.AddCheatXpAsync(1000);
            RefreshLobbyTopBar();
            RefreshDeveloperCheatOverlay("Added 1000 XP.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Developer cheat add XP failed: " + ex);
            RefreshDeveloperCheatOverlay("Could not add XP.");
        }
        finally
        {
            RefreshDeveloperCheatOverlay();
        }
    }

    async void OnDeveloperCheatUnlockBlueprintsClicked()
    {
        if (PlayerProfileService.Instance == null || developerCheatOverlayObject == null)
            return;

        try
        {
            RefreshDeveloperCheatOverlay("Unlocking blueprints...", true);
            await PlayerProfileService.Instance.UnlockAllBlueprintsAsync();
            RefreshDeveloperCheatOverlay("All blueprints unlocked.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Developer cheat unlock blueprints failed: " + ex);
            RefreshDeveloperCheatOverlay("Could not unlock blueprints.");
        }
        finally
        {
            RefreshDeveloperCheatOverlay();
        }
    }

    async void OnDeveloperCheatLockBlueprintsClicked()
    {
        if (PlayerProfileService.Instance == null || developerCheatOverlayObject == null)
            return;

        try
        {
            RefreshDeveloperCheatOverlay("Locking blueprints...", true);
            await PlayerProfileService.Instance.LockAllBlueprintsAsync();
            RefreshDeveloperCheatOverlay("Optional blueprints locked.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Developer cheat lock blueprints failed: " + ex);
            RefreshDeveloperCheatOverlay("Could not lock blueprints.");
        }
        finally
        {
            RefreshDeveloperCheatOverlay();
        }
    }

    async void OnDeveloperCheatUnlockShipsClicked()
    {
        if (PlayerProfileService.Instance == null || developerCheatOverlayObject == null)
            return;

        try
        {
            RefreshDeveloperCheatOverlay("Unlocking ships...", true);
            await PlayerProfileService.Instance.UnlockAllShipsAsync();
            RefreshDeveloperCheatOverlay("All ships unlocked.");
            PlayerProfilePanelUI.RefreshOpenPanel();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Developer cheat unlock ships failed: " + ex);
            RefreshDeveloperCheatOverlay("Could not unlock ships.");
        }
        finally
        {
            RefreshDeveloperCheatOverlay();
        }
    }

    async void OnDeveloperCheatLockShipsClicked()
    {
        if (PlayerProfileService.Instance == null || developerCheatOverlayObject == null)
            return;

        try
        {
            RefreshDeveloperCheatOverlay("Locking ships...", true);
            await PlayerProfileService.Instance.LockAllShipsAsync();
            RefreshDeveloperCheatOverlay("Optional ships locked.");
            PlayerProfilePanelUI.RefreshOpenPanel();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Developer cheat lock ships failed: " + ex);
            RefreshDeveloperCheatOverlay("Could not lock ships.");
        }
        finally
        {
            RefreshDeveloperCheatOverlay();
        }
    }

    async void OnDeveloperCheatUnlockMapsClicked()
    {
        if (PlayerProfileService.Instance == null || developerCheatOverlayObject == null)
            return;

        try
        {
            RefreshDeveloperCheatOverlay("Unlocking maps...", true);
            await PlayerProfileService.Instance.UnlockAllMapsAsync();
            RefreshHostSettingsUi();
            RefreshLobbyScreenContent();
            RefreshDeveloperCheatOverlay("All maps unlocked.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Developer cheat unlock maps failed: " + ex);
            RefreshDeveloperCheatOverlay("Could not unlock maps.");
        }
        finally
        {
            RefreshDeveloperCheatOverlay();
        }
    }

    async void OnDeveloperCheatLockMapsClicked()
    {
        if (PlayerProfileService.Instance == null || developerCheatOverlayObject == null)
            return;

        try
        {
            RefreshDeveloperCheatOverlay("Locking maps...", true);
            await PlayerProfileService.Instance.LockAllMapsAsync();
            EnsureActiveRoomMapUnlockedAfterMapProgressChange();
            RefreshHostSettingsUi();
            RefreshLobbyScreenContent();
            RefreshDeveloperCheatOverlay("Optional maps locked.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Developer cheat lock maps failed: " + ex);
            RefreshDeveloperCheatOverlay("Could not lock maps.");
        }
        finally
        {
            RefreshDeveloperCheatOverlay();
        }
    }

    async void OnDeveloperCheatUnlockProjectsClicked()
    {
        if (PlayerProfileService.Instance == null || developerCheatOverlayObject == null)
            return;

        try
        {
            RefreshDeveloperCheatOverlay("Unlocking projects...", true);
            await PlayerProfileService.Instance.UnlockAllProjectsAsync();
            PlayerProfilePanelUI.RefreshOpenPanel();
            RefreshDeveloperCheatOverlay("All projects unlocked.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Developer cheat unlock projects failed: " + ex);
            RefreshDeveloperCheatOverlay("Could not unlock projects.");
        }
        finally
        {
            RefreshDeveloperCheatOverlay();
        }
    }

    async void OnDeveloperCheatLockProjectsClicked()
    {
        if (PlayerProfileService.Instance == null || developerCheatOverlayObject == null)
            return;

        try
        {
            RefreshDeveloperCheatOverlay("Locking projects...", true);
            await PlayerProfileService.Instance.LockAllProjectsAsync();
            PlayerProfilePanelUI.RefreshOpenPanel();
            RefreshDeveloperCheatOverlay("Project unlocks restored.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Developer cheat lock projects failed: " + ex);
            RefreshDeveloperCheatOverlay("Could not lock projects.");
        }
        finally
        {
            RefreshDeveloperCheatOverlay();
        }
    }

    void EnsureActiveRoomMapUnlockedAfterMapProgressChange()
    {
        if (!PhotonNetwork.IsMasterClient || !PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return;

        LobbyMapDefinition activeMap = LobbyMapCatalog.Get(RoomSettings.GetSelectedLobbyMapId());
        if (activeMap != null && IsMapUnlockedForLocalPlayer(activeMap))
            return;

        LobbyMapDefinition fallbackMap = LobbyMapCatalog.GetDefault();
        if (fallbackMap == null || !IsMapUnlockedForLocalPlayer(fallbackMap))
            fallbackMap = LobbyMapCatalog.Get(LobbyMapCatalog.JustSpaceMapId);

        if (fallbackMap == null)
            return;

        selectedMapId = fallbackMap.Id;
        Hashtable props = new Hashtable();
        LobbyMapCatalog.ApplyToProperties(fallbackMap, props);
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    void OnDeveloperCheatResetAccountClicked()
    {
        if (developerCheatResetConfirmObject == null)
            return;

        developerCheatResetConfirmObject.SetActive(true);
        EnsureDeveloperCheatLayering();
    }

    async void OnDeveloperCheatResetConfirmClicked()
    {
        if (PlayerProfileService.Instance == null || developerCheatOverlayObject == null)
            return;

        try
        {
            RefreshDeveloperCheatOverlay("Resetting account...", true);
            await PlayerProfileService.Instance.ResetAccountAsync();
            HideDeveloperCheatResetConfirm();
            EnsureActiveRoomMapUnlockedAfterMapProgressChange();
            RefreshLobbyTopBar();
            RefreshHostSettingsUi();
            RefreshLobbyScreenContent();
            RefreshDeveloperCheatOverlay("Account reset.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Developer cheat reset account failed: " + ex);
            RefreshDeveloperCheatOverlay("Account reset failed.");
        }
        finally
        {
            RefreshDeveloperCheatOverlay();
        }
    }

    void OnLaunchClicked()
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning("LAUNCH clicked, but Photon is not in a room yet.");
            return;
        }

        bool alreadyStarted = IsCurrentRoomGameStarted();
        string sessionState = RoomSettings.GetSessionState();
        if (sessionState != RoomSettings.SessionStateInLobby && !alreadyStarted)
        {
            Debug.LogWarning("LAUNCH clicked, but room is not in lobby state: " + sessionState);
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            StartGame();
            EnsureLaunchStartRecovery();
            return;
        }

        SetReady(true);
    }

    bool IsCurrentRoomGameStarted()
    {
        return PhotonNetwork.CurrentRoom != null &&
               PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
               value is bool started &&
               started;
    }

    void EnsureLaunchStartRecovery()
    {
        if (launchStartRecoveryRoutine != null)
            return;

        launchStartRecoveryRoutine = StartCoroutine(RecoverLaunchStartRoutine());
    }

    System.Collections.IEnumerator RecoverLaunchStartRoutine()
    {
        const int maxAttempts = 20;
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(0.1f);

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (!PhotonNetwork.InRoom)
                break;

            if (IsCurrentRoomGameStarted())
            {
                HideLobby();
                NetworkManager manager = FindAnyObjectByType<NetworkManager>();
                if (manager != null)
                    manager.RestoreRoomStateAfterSceneLoad();

                launchStartRecoveryRoutine = null;
                yield break;
            }

            if (PhotonNetwork.IsMasterClient && attempt == 5 && RoomSettings.GetSessionState() == RoomSettings.SessionStateInLobby)
            {
                Debug.LogWarning("LAUNCH recovery: gameStarted was not visible after 0.5s, retrying StartGame.");
                StartGame();
            }

            yield return wait;
        }

        launchStartRecoveryRoutine = null;
    }

    Button GetEnemySettingButton(EnemyBotKind kind, string suffix)
    {
        enemySettingButtons.TryGetValue(GetEnemySettingUiKey(kind, suffix), out Button button);
        return button;
    }

    bool ContainsEnemyRoomSettingChange(Hashtable changedProps)
    {
        foreach (System.Collections.DictionaryEntry entry in changedProps)
        {
            if (entry.Key is string key &&
                (key.StartsWith("enemy.", System.StringComparison.Ordinal) ||
                 key == RoomSettings.EnemyBotsEnabledKey ||
                 key == RoomSettings.CorsairEnabledKey ||
                 key == RoomSettings.CorsairSpawnSecondKey ||
                 key == RoomSettings.CorsairHpKey))
            {
                return true;
            }
        }

        return false;
    }

    bool ContainsMapEffectChanceRoomSettingChange(Hashtable changedProps)
    {
        if (changedProps == null)
            return false;

        foreach (System.Collections.DictionaryEntry entry in changedProps)
        {
            if (entry.Key is string key && RoomSettings.IsMapEffectChanceKey(key))
                return true;
        }

        return false;
    }

    bool ContainsLobbySettingChange(Hashtable changedProps)
    {
        if (changedProps == null)
            return false;

        return changedProps.ContainsKey(RoomSettings.RoundDurationKey) ||
               changedProps.ContainsKey(RoomSettings.MapSizeKey) ||
               changedProps.ContainsKey(RoomSettings.ToxicBordersEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.MapBackgroundKey) ||
               changedProps.ContainsKey(RoomSettings.SelectedMapKey) ||
               changedProps.ContainsKey(RoomSettings.VisualEffectsEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.AdvancedSpawnVfxEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.LowHpHullSparksEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.BoomVfxEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.DynamicCameraZoomEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.ParallaxBackgroundKey) ||
               changedProps.ContainsKey(RoomSettings.BackgroundObjectKey) ||
               changedProps.ContainsKey(RoomSettings.GravityWellPhysicsEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.AvengerPlotEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.ViperPlotChancePercentKey) ||
               changedProps.ContainsKey(RoomSettings.ArrowPlotChancePercentKey) ||
               changedProps.ContainsKey(RoomSettings.BisonPlotChancePercentKey) ||
               changedProps.ContainsKey(RoomSettings.InvaderPlotChancePercentKey) ||
               changedProps.ContainsKey(RoomSettings.EndDisasterModeKey) ||
               changedProps.ContainsKey(RoomSettings.EndDisasterWarningSecondsKey) ||
               changedProps.ContainsKey(RoomSettings.ObstacleDensityKey) ||
               changedProps.ContainsKey(RoomSettings.ObstacleDestroyEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.ObstacleHpKey) ||
               changedProps.ContainsKey(RoomSettings.ObstacleSizePercentKey) ||
               changedProps.ContainsKey(RoomSettings.ObstacleNoBordersKey) ||
               changedProps.ContainsKey(RoomSettings.TreasureDensityKey) ||
               changedProps.ContainsKey(RoomSettings.RadioactiveTreasureDensityKey) ||
               changedProps.ContainsKey(RoomSettings.AlienSecretsDensityKey) ||
               changedProps.ContainsKey(RoomSettings.ResourceRichnessKey) ||
               changedProps.ContainsKey(RoomSettings.CrazyEnemiesModeKey) ||
               changedProps.ContainsKey(RoomSettings.CrazyEnemiesStartUtcMsKey) ||
               changedProps.ContainsKey(RoomSettings.CrazyEnemiesActiveKey) ||
               changedProps.ContainsKey(RoomSettings.FogOfWarModeKey) ||
               changedProps.ContainsKey(RoomSettings.FogOfWarStartUtcMsKey) ||
               changedProps.ContainsKey(RoomSettings.FogOfWarActiveKey) ||
               changedProps.ContainsKey(RoomSettings.PirateBaseModeKey) ||
               changedProps.ContainsKey(RoomSettings.PirateBaseStartUtcMsKey) ||
               changedProps.ContainsKey(RoomSettings.PirateBaseActiveKey) ||
               changedProps.ContainsKey(RoomSettings.AsteroidShowerModeKey) ||
               changedProps.ContainsKey(RoomSettings.AsteroidShowerStartUtcMsKey) ||
               changedProps.ContainsKey(RoomSettings.AsteroidShowerActiveKey) ||
               changedProps.ContainsKey(RoomSettings.CosmicWormModeKey) ||
               changedProps.ContainsKey(RoomSettings.CosmicWormStartUtcMsKey) ||
               changedProps.ContainsKey(RoomSettings.CosmicWormActiveKey) ||
               changedProps.ContainsKey(RoomSettings.MilitaryConvoyModeKey) ||
               changedProps.ContainsKey(RoomSettings.MilitaryConvoyStartUtcMsKey) ||
               changedProps.ContainsKey(RoomSettings.MilitaryConvoyActiveKey) ||
               ContainsMapEffectChanceRoomSettingChange(changedProps) ||
               changedProps.ContainsKey(RoomSettings.SpaceJunkDensityKey) ||
               changedProps.ContainsKey(RoomSettings.ContainersDensityKey) ||
               changedProps.ContainsKey(RoomSettings.ArtifactAsteroidsDensityKey) ||
               changedProps.ContainsKey(RoomSettings.RandomLootWreckCountKey) ||
               changedProps.ContainsKey(RoomSettings.HiddenTreasureEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.NebulaDensityKey) ||
               changedProps.ContainsKey(RoomSettings.FireNebulaDensityKey) ||
               changedProps.ContainsKey(RoomSettings.ToxicNebulaDensityKey) ||
               changedProps.ContainsKey(RoomSettings.NebulaSizeKey) ||
               changedProps.ContainsKey(RoomSettings.FireNebulaSizeKey) ||
               changedProps.ContainsKey(RoomSettings.ToxicNebulaSizeKey) ||
               changedProps.ContainsKey(RoomSettings.AdvancedNebulaEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.CloudsDensityKey) ||
               changedProps.ContainsKey(RoomSettings.CloudsSizeKey) ||
               changedProps.ContainsKey(RoomSettings.ExtractionCountKey) ||
               changedProps.ContainsKey(RoomSettings.ExtractionTypeKey) ||
               changedProps.ContainsKey(RoomSettings.RepairBayCountKey) ||
               changedProps.ContainsKey(RoomSettings.SpaceFactoryCountKey) ||
               changedProps.ContainsKey(RoomSettings.ScienceStationCountKey) ||
               changedProps.ContainsKey(RoomSettings.BoosterSlowdownKey) ||
               changedProps.ContainsKey(RoomSettings.BoosterRecoveryDelayKey) ||
               changedProps.ContainsKey(RoomSettings.AdvancedBoosterEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.ShipDriftEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.LastShipTimerMultiplierKey) ||
               changedProps.ContainsKey(RoomSettings.InventoryLossEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.EquipmentLossEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.CosmicWormEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.MovingObjectsEnabledKey) ||
               ContainsEnemyRoomSettingChange(changedProps) ||
               changedProps.ContainsKey(RoomSettings.BulletPushMultiplierKey) ||
               changedProps.ContainsKey(RoomSettings.BatteringDamageKey) ||
               changedProps.ContainsKey(RoomSettings.CollectKeepAliveRangeBonusPercentKey) ||
               changedProps.ContainsKey(RoomSettings.HapticsEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.FpsCounterEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.NeutralRidersEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.NeutralRidersCountKey) ||
               changedProps.ContainsKey(RoomSettings.NeutralRidersAggressionKey) ||
               ContainsGunSetupRoomSettingChange(changedProps) ||
               changedProps.ContainsKey(RoomSettings.ObstacleWeightFactorKey) ||
               changedProps.ContainsKey(RoomSettings.TreasureWeightFactorKey);
    }

    bool ContainsGunSetupRoomSettingChange(Hashtable changedProps)
    {
        if (changedProps == null)
            return false;

        foreach (System.Collections.DictionaryEntry entry in changedProps)
        {
            if (entry.Key is string key && RoomSettings.IsGunSetupKey(key))
                return true;
        }

        return false;
    }

    float GetRoundDuration()
    {
        return RoomSettings.GetRoundDuration();
    }

    string GetObstacleDensity()
    {
        return GetDensitySetting(RoomSettings.ObstacleDensityKey);
    }

    bool AreObstaclesDestructible()
    {
        return RoomSettings.AreObstaclesDestructible();
    }

    int GetObstacleHp()
    {
        return RoomSettings.GetObstacleHp();
    }

    int GetObstacleSizePercent()
    {
        return RoomSettings.GetObstacleSizePercent();
    }

    bool AreObstaclesBorderless()
    {
        return RoomSettings.AreObstaclesBorderless();
    }

    string GetMapSize()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.MapSizeKey, out object value) &&
            value is string mode)
        {
            return mode;
        }

        return RoomSettings.DefaultMapSize;
    }

    int GetMapBackground()
    {
        return RoomSettings.GetMapBackgroundIndex();
    }

    string GetParallaxBackground()
    {
        return RoomSettings.GetParallaxBackgroundId();
    }

    string GetBackgroundObject()
    {
        return RoomSettings.GetBackgroundObjectId();
    }

    bool AreVisualEffectsEnabled()
    {
        return RoomSettings.AreVisualEffectsEnabled();
    }

    bool IsDynamicCameraZoomEnabled()
    {
        return RoomSettings.IsDynamicCameraZoomEnabled();
    }

    string GetEndDisasterMode()
    {
        return RoomSettings.GetEndDisasterMode();
    }

    int GetEndDisasterWarningSeconds()
    {
        return RoomSettings.GetEndDisasterWarningSeconds();
    }

    string GetTreasureDensity()
    {
        return RoomSettings.GetBaseTreasureDensity();
    }

    string GetRadioactiveTreasureDensity()
    {
        return RoomSettings.GetRadioactiveTreasureDensity();
    }

    string GetAlienSecretsDensity()
    {
        return RoomSettings.GetAlienSecretsDensity();
    }

    string GetResourceRichness()
    {
        return RoomSettings.GetBaseResourceRichness();
    }

    string GetSpaceJunkDensity()
    {
        return RoomSettings.GetSpaceJunkDensity();
    }

    string GetContainersDensity()
    {
        return RoomSettings.GetContainersDensity();
    }

    string GetArtifactAsteroidsDensity()
    {
        return RoomSettings.GetArtifactAsteroidsDensity();
    }

    int GetRandomLootWreckCount()
    {
        return RoomSettings.GetRandomLootWreckCount();
    }

    string GetNebulaDensity()
    {
        return GetDensitySetting(RoomSettings.NebulaDensityKey);
    }

    string GetFireNebulaDensity()
    {
        return RoomSettings.GetFireNebulaDensity();
    }

    string GetToxicNebulaDensity()
    {
        return RoomSettings.GetToxicNebulaDensity();
    }

    string GetNebulaSize()
    {
        return RoomSettings.GetNebulaSize();
    }

    string GetFireNebulaSize()
    {
        return RoomSettings.GetFireNebulaSize();
    }

    string GetToxicNebulaSize()
    {
        return RoomSettings.GetToxicNebulaSize();
    }

    string GetCloudsDensity()
    {
        return RoomSettings.GetCloudsDensity();
    }

    string GetCloudsSize()
    {
        return RoomSettings.GetCloudsSize();
    }

    int GetExtractionCount()
    {
        return RoomSettings.GetExtractionCount();
    }

    string GetExtractionType()
    {
        return RoomSettings.GetExtractionType();
    }

    int GetRepairBayCount()
    {
        return RoomSettings.GetRepairBayCount();
    }

    int GetSpaceFactoryCount()
    {
        return RoomSettings.GetSpaceFactoryCount();
    }

    int GetScienceStationCount()
    {
        return RoomSettings.GetScienceStationCount();
    }

    int GetBoosterSlowdownPercent()
    {
        return RoomSettings.GetBoosterSlowdownPercent();
    }

    int GetBoosterRecoveryDelay()
    {
        return RoomSettings.GetBoosterRecoveryDelay();
    }

    int GetShipDriftLevel()
    {
        return RoomSettings.GetShipDriftLevel();
    }

    float GetLastShipTimerMultiplier()
    {
        return RoomSettings.GetLastShipTimerMultiplier();
    }

    bool AreMovingObjectsEnabled()
    {
        return RoomSettings.AreMovingObjectsEnabled();
    }

    string GetMovingObjectsMode()
    {
        return RoomSettings.GetMovingObjectsMode();
    }

    bool AreEnemyBotsEnabled()
    {
        return RoomSettings.AreEnemyBotsEnabled();
    }

    bool AreCorsairsEnabled()
    {
        return RoomSettings.AreCorsairsEnabled();
    }

    int GetCorsairSpawnSecond()
    {
        return RoomSettings.GetCorsairSpawnSecond();
    }

    int GetCorsairHp()
    {
        return RoomSettings.GetCorsairHp();
    }

    int GetBulletPushMultiplier()
    {
        return RoomSettings.GetBulletPushMultiplier();
    }

    int GetBatteringDamage()
    {
        return RoomSettings.GetBatteringDamage();
    }

    int GetObstacleWeightFactor()
    {
        return RoomSettings.GetObstacleWeightFactor();
    }

    int GetTreasureWeightFactor()
    {
        return RoomSettings.GetTreasureWeightFactor();
    }

    string GetDensitySetting(string key)
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object value) &&
            value is string density)
        {
            return density;
        }

        return "medium";
    }

    string FormatRoundDuration(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.RoundToInt(seconds % 60f);
        return minutes + ":" + secs.ToString("00");
    }

    string FormatDensity(string density)
    {
        return density.Replace("_", " ").ToUpperInvariant();
    }

    string FormatNebulaSize(string size)
    {
        return RoomSettings.NormalizeNebulaSize(size).Replace("_", " ").ToUpperInvariant();
    }

    string FormatResourceRichness(string richness)
    {
        return RoomSettings.NormalizeResourceRichness(richness).Replace("_", " ").ToUpperInvariant();
    }

    string FormatRadioactiveTreasureDensity(string density)
    {
        return RoomSettings.NormalizeRadioactiveTreasureDensity(density).ToUpperInvariant();
    }

    string FormatAlienSecretsDensity(string density)
    {
        return RoomSettings.NormalizeAlienSecretsDensity(density).Replace("_", " ").ToUpperInvariant();
    }

    string FormatMapEffectSetting(string modeKey, string startKey, string activeKey)
    {
        if (IsMapEffectActive(activeKey))
            return "ACTIVE";

        string mode = RoomSettings.GetMapEffectMode(modeKey);
        if (mode == RoomSettings.MapEffectModeAlwaysOn)
            return "ALWAYS ON";

        if (mode == RoomSettings.MapEffectModeUtcStart)
        {
            double startUtcMs = RoomSettings.GetMapEffectStartUtcMs(startKey);
            if (startUtcMs >= 0d)
            {
                System.DateTimeOffset start = System.DateTimeOffset.FromUnixTimeMilliseconds((long)startUtcMs);
                return "UTC " + start.ToString("MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return "OFF";
    }

    bool IsMapEffectActive(string activeKey)
    {
        return PhotonNetwork.CurrentRoom != null &&
               PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(activeKey, out object value) &&
               value is bool active &&
               active;
    }

    string FormatSpaceJunkDensity(string density)
    {
        return RoomSettings.NormalizeSpaceJunkDensity(density).ToUpperInvariant();
    }

    string FormatContainersDensity(string density)
    {
        return RoomSettings.NormalizeContainersDensity(density).Replace("_", " ").ToUpperInvariant();
    }

    string FormatArtifactAsteroidsDensity(string density)
    {
        return RoomSettings.NormalizeArtifactAsteroidsDensity(density).Replace("_", " ").ToUpperInvariant();
    }

    string FormatRandomLootWreckCount(int count)
    {
        return count <= 0 ? "OFF" : Mathf.Clamp(count, 1, 5).ToString();
    }

    string FormatExtractionType(string extractionType)
    {
        switch (RoomSettings.NormalizeExtractionType(extractionType))
        {
            case RoomSettings.ExtractionTypeCarrier:
                return "CARRIER";
            case RoomSettings.ExtractionTypeSpaceCity:
                return "SPACE CITY";
            case RoomSettings.ExtractionTypeAncientPortal:
                return "ANCIENT PORTAL";
            default:
                return "PORTAL";
        }
    }

    string FormatMapSize(string mapSize)
    {
        switch (mapSize)
        {
            case "very_large":
                return "VERY LARGE";
            case "super_large":
                return "SUPER LARGE";
            default:
                return mapSize.ToUpperInvariant();
        }
    }

    string FormatMapBackground(int backgroundIndex)
    {
        return "TLO " + Mathf.Clamp(backgroundIndex, 1, RoomSettings.MaxMapBackground);
    }

    string FormatParallaxBackground(string backgroundId)
    {
        switch (RoomSettings.NormalizeParallaxBackgroundId(backgroundId))
        {
            case RoomSettings.ParallaxBackgroundKosmos3:
                return "KOSMOS 3";
            case RoomSettings.ParallaxBackgroundKosmos6:
                return "KOSMOS 6";
            case RoomSettings.ParallaxBackgroundKosmos8:
                return "KOSMOS 8";
            case RoomSettings.ParallaxBackgroundKosmos9:
                return "KOSMOS 9";
            case RoomSettings.ParallaxBackgroundKosmos10:
                return "KOSMOS 10";
            case RoomSettings.ParallaxBackgroundKosmos11:
                return "KOSMOS 11";
            case RoomSettings.ParallaxBackgroundKosmos12:
                return "KOSMOS 12";
            case RoomSettings.ParallaxBackgroundKosmos13:
                return "KOSMOS 13";
            case RoomSettings.ParallaxBackgroundKosmos14:
                return "KOSMOS 14";
            case RoomSettings.ParallaxBackgroundKosmos15:
                return "KOSMOS 15";
            case RoomSettings.ParallaxBackgroundKosmos16:
                return "KOSMOS 16";
            default:
                return FormatParallaxBackground(RoomSettings.DefaultParallaxBackground);
        }
    }

    string FormatBackgroundObject(string objectId)
    {
        switch (RoomSettings.NormalizeBackgroundObjectId(objectId))
        {
            case RoomSettings.BackgroundObject1:
                return "BACKGROUND OBJECT 1";
            case RoomSettings.BackgroundObject2:
                return "BACKGROUND OBJECT 2";
            case RoomSettings.BackgroundObject3:
                return "BACKGROUND OBJECT 3";
            case RoomSettings.BackgroundObject4:
                return "BACKGROUND OBJECT 4";
            case RoomSettings.BackgroundObject5:
                return "BACKGROUND OBJECT 5";
            case RoomSettings.BackgroundObject6:
                return "BACKGROUND OBJECT 6";
            case RoomSettings.BackgroundObject7:
                return "BACKGROUND OBJECT 7";
            case RoomSettings.BackgroundObject8:
                return "BACKGROUND OBJECT 8";
            case RoomSettings.BackgroundObject9:
                return "BACKGROUND OBJECT 9";
            case RoomSettings.BackgroundObject10:
                return "BACKGROUND OBJECT 10";
            case RoomSettings.BackgroundObject11:
                return "BACKGROUND OBJECT 11";
            case RoomSettings.BackgroundObject12:
                return "BACKGROUND OBJECT 12";
            case RoomSettings.BackgroundObject13:
                return "BACKGROUND OBJECT 13";
            case RoomSettings.BackgroundObject14:
                return "BACKGROUND OBJECT 14";
            default:
                return "OFF";
        }
    }

    string GetDisplayName(Player player)
    {
        if (player == null)
            return "Unknown";

        if (!string.IsNullOrWhiteSpace(player.NickName))
            return player.NickName;

        return "Player " + player.ActorNumber;
    }
}
