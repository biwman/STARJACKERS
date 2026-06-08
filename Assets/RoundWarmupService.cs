using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public sealed class RoundWarmupService : MonoBehaviour
{
    const string ObstacleLayoutKey = "obstacleLayout";
    const string ExtractionLayoutKey = "extractionLayout";
    const string NebulaLayoutKey = "nebulaLayout";
    const string FireNebulaLayoutKey = NebulaSpawner.FireNebulaLayoutKey;
    const string ToxicNebulaLayoutKey = NebulaSpawner.ToxicNebulaLayoutKey;
    const string CloudLayoutKey = NebulaSpawner.CloudLayoutKey;
    const string CloudDirectionKey = NebulaSpawner.CloudDirectionKey;
    const string RepairBayLayoutKey = "repairBayLayout";
    const string SpaceFactoryLayoutKey = SpaceFactorySpawner.LayoutKey;
    const string ScienceStationLayoutKey = ScienceStationSpawner.LayoutKey;
    const string ArtifactAsteroidLayoutKey = ArtifactAsteroidSpawner.LayoutKey;
    const string LoneShipModeStartTimeKey = "loneShipModeStartTime";
    const float MaxNetworkWarmupWaitSeconds = 5f;

    static readonly string[] PhotonPrefabResourcePaths =
    {
        "Player",
        "Bullet",
        "TreasureNetwork",
        "DroppedCargoCrate",
        "ExtractionZone",
        "VFX/vfx_Explosion_01"
    };

    static readonly string[] CommonRoundSpriteResourcePaths =
    {
        "space_factory",
        "science_station",
        "stacja_naprawcza_resource",
        "portal_alarm_light",
        "skrzynia_metal_clean_resource",
        "Items/escape_pod",
        "lure_beacon_onmap_resource",
        "Visuals/Clouds/cloud_topdown_cumulus_broad",
        "Visuals/Clouds/cloud_topdown_front_band",
        "Visuals/Clouds/cloud_topdown_scattered_cluster",
        "Visuals/Clouds/cloud_topdown_wispy_sheet",
        "nebula_variant_01",
        "nebula_variant_02",
        "nebula_variant_03",
        "nebula_variant_04",
        "nebula_variant_05",
        "nebula_variant_06",
        "nebula_variant_07",
        "nebula_variant_08",
        "nebula_variant_09",
        "fire_nebula_variant_01",
        "fire_nebula_variant_02",
        "fire_nebula_variant_03",
        "fire_nebula_variant_04",
        "toxic_nebula_variant_01",
        "koszmiczna_anomalia_resource",
        "nebula_frayed_resource",
        "Visuals/Backgrounds/background_object13_addon_resource",
        "Visuals/Obstacles/ArtifactAsteroids/artifact_asteroid_01_inactive",
        "Visuals/Obstacles/ArtifactAsteroids/artifact_asteroid_01_active",
        "Visuals/Obstacles/ArtifactAsteroids/artifact_asteroid_02_inactive",
        "Visuals/Obstacles/ArtifactAsteroids/artifact_asteroid_02_active",
        "Visuals/Obstacles/ArtifactAsteroids/artifact_asteroid_03_inactive",
        "Visuals/Obstacles/ArtifactAsteroids/artifact_asteroid_03_active",
        "Visuals/Obstacles/ArtifactAsteroids/artifact_asteroid_04_inactive",
        "Visuals/Obstacles/ArtifactAsteroids/artifact_asteroid_04_active",
        "Visuals/Obstacles/ArtifactAsteroids/artifact_asteroid_05_inactive",
        "Visuals/Obstacles/ArtifactAsteroids/artifact_asteroid_05_active",
        "Visuals/Obstacles/ArtifactAsteroids/artifact_asteroid_06_inactive",
        "Visuals/Obstacles/ArtifactAsteroids/artifact_asteroid_06_active"
    };

    static RoundWarmupService instance;

    string warmedRoundToken = string.Empty;
    string runningRoundToken = string.Empty;
    string observedRoundToken = string.Empty;
    string readyMarkedRoundToken = string.Empty;
    string displayedCurtainRoundToken = string.Empty;
    string committedRoundToken = string.Empty;
    bool warmupRunning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("RoundWarmupService");
        instance = root.AddComponent<RoundWarmupService>();
        DontDestroyOnLoad(root);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (!TryGetCurrentWarmupToken(out string roundToken))
            return;

        if (!string.Equals(observedRoundToken, roundToken, StringComparison.Ordinal))
        {
            observedRoundToken = roundToken;
            readyMarkedRoundToken = string.Empty;
            committedRoundToken = string.Empty;
            ResetObservedRoundState();
            MarkLocalWarmupPending(roundToken);
        }

        if (!string.Equals(displayedCurtainRoundToken, roundToken, StringComparison.Ordinal))
        {
            displayedCurtainRoundToken = roundToken;
            RoundStartCurtainUI.ShowForRoundStart(roundToken);
        }

        if (!warmupRunning && !string.Equals(warmedRoundToken, roundToken, StringComparison.Ordinal))
            StartCoroutine(WarmupRound(roundToken));

        if (PhotonNetwork.IsMasterClient && IsRoundPreparing())
            TryCommitPreparedRoundStart(roundToken);
    }

    static void ResetObservedRoundState()
    {
        GameplayHudVisibility.ResetSuppression();
        EarlyRoundExitUI.HideAll();
        RoundPilotHudUI.DestroyAllRuntimeObjects();
        ShipDamageState.ClearAllRuntimeDamage();
        TreasureCollector.ResetRoundReservations();
        RoundResultsTracker.ResetForCurrentRoom();
        ResetRoundTransientEffects();
    }

    public static void ResetRoundTransientEffects()
    {
        VectorWeakPointMemory.ResetForSessionTransition();
        PilotActiveAbilityController.ResetForSessionTransition();
        AstroCutterBeamVfx.ResetForSessionTransition();
        SpaceTrapTarget.ResetForSessionTransition();
        TractorBeamVfx.ResetForSessionTransition();
    }

    public static void BeginRoundStartPreparation()
    {
        EnsureInstance();

        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        if (IsRoomGameStarted())
            return;

        string sessionState = RoomSettings.GetSessionState();
        if (sessionState == RoomSettings.SessionStatePreparing)
            return;

        GameplayHudVisibility.ResetSuppression();
        EarlyRoundExitUI.HideAll();
        ShipDamageState.ClearAllRuntimeDamage();
        TreasureCollector.ResetRoundReservations();
        ResetRoundTransientEffects();

        PhotonNetwork.CurrentRoom.IsOpen = false;

        string roundToken = BuildNewWarmupToken();
        RoundStartCurtainUI.ShowForRoundStart(roundToken);
        MarkLocalWarmupPending(roundToken);

        Hashtable props = BuildRoundPreparationProperties(roundToken);
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RoundResultsTracker.ResetForCurrentRoom();
    }

    public static bool IsReadyForCurrentRound()
    {
        EnsureInstance();

        if (!TryGetCurrentWarmupToken(out string roundToken))
            return true;

        return instance != null &&
               !instance.warmupRunning &&
               string.Equals(instance.warmedRoundToken, roundToken, StringComparison.Ordinal);
    }

    public static bool HasWarmupRoundContext()
    {
        return TryGetCurrentWarmupToken(out _);
    }

    public static string GetCurrentDisplayToken()
    {
        return TryGetCurrentWarmupToken(out string token) ? token : string.Empty;
    }

    public static bool IsRoundPreparing()
    {
        return PhotonNetwork.CurrentRoom != null &&
               string.Equals(RoomSettings.GetSessionState(), RoomSettings.SessionStatePreparing, StringComparison.Ordinal);
    }

    public static bool IsRoundStarted()
    {
        return IsRoomGameStarted();
    }

    static void EnsureInstance()
    {
        if (instance != null)
            return;

        if (!Application.isPlaying)
            return;

        GameObject root = new GameObject("RoundWarmupService");
        instance = root.AddComponent<RoundWarmupService>();
        DontDestroyOnLoad(root);
    }

    IEnumerator WarmupRound(string roundToken)
    {
        warmupRunning = true;
        runningRoundToken = roundToken;
        float startedAt = Time.realtimeSinceStartup;

        yield return WarmStep("audio", () => _ = AudioManager.Instance);
        yield return WarmStep("ui", () =>
        {
            UIRuntimeStyler.PrewarmRuntimeSprites();
            InventoryItemCatalog.PrewarmIcons();
        });
        yield return WarmStep("photon prefabs", PrewarmPhotonPrefabs);
        yield return WarmStep("round sprites", () => PrewarmResourceSprites(CommonRoundSpriteResourcePaths));
        yield return WarmEnemyAssets();
        yield return WarmStep("projectiles", Bullet.PrewarmRoundAssets);
        yield return WarmStep("deployables", PlayerDeployableRuntime.PrewarmRoundAssets);
        yield return WarmStep("dropped cargo", DroppedCargoCrate.PrewarmRoundAssets);
        yield return WarmStep("vfx", PrewarmVfx);

        warmedRoundToken = roundToken;
        warmupRunning = false;
        runningRoundToken = string.Empty;
        MarkLocalWarmupReady(roundToken);

        float elapsedMs = (Time.realtimeSinceStartup - startedAt) * 1000f;
        Debug.Log("RoundWarmupService: warmed round assets in " + elapsedMs.ToString("0.0") + " ms.");
    }

    IEnumerator WarmStep(string label, Action action)
    {
        try
        {
            action?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("RoundWarmupService: warmup step failed (" + label + "): " + ex.Message);
        }

        yield return null;
    }

    IEnumerator WarmEnemyAssets()
    {
        IReadOnlyList<EnemyBotDefinition> definitions = EnemyBotCatalog.AllDefinitions;
        for (int i = 0; i < definitions.Count; i++)
        {
            try
            {
                PrewarmEnemyDefinition(definitions[i]);
            }
            catch (Exception ex)
            {
                string id = definitions[i] != null ? definitions[i].Id : "unknown";
                Debug.LogWarning("RoundWarmupService: enemy warmup failed (" + id + "): " + ex.Message);
            }

            if ((i + 1) % 2 == 0)
                yield return null;
        }

        yield return WarmStep("enemy specials", PrewarmEnemySpecialAssets);
    }

    static bool TryGetCurrentWarmupToken(out string token)
    {
        token = string.Empty;

        if (PhotonNetwork.CurrentRoom == null)
            return false;

        bool preparing = IsRoundPreparing();
        bool started = IsRoomGameStarted();
        if (!preparing && !started)
            return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.RoundWarmupTokenKey, out object warmupValue) &&
            warmupValue is string warmupToken &&
            !string.IsNullOrWhiteSpace(warmupToken))
        {
            token = warmupToken;
            return true;
        }

        string startValue = "nostart";
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.StartTimeKey, out object value) && value != null)
            startValue = value.ToString();

        token = PhotonNetwork.CurrentRoom.Name + "_" + startValue;
        return true;
    }

    static string BuildNewWarmupToken()
    {
        string roomName = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : "room";
        return roomName + "_warmup_" + PhotonNetwork.Time.ToString("F3", CultureInfo.InvariantCulture);
    }

    static Hashtable BuildRoundPreparationProperties(string roundToken)
    {
        return new Hashtable
        {
            ["gameStarted"] = false,
            [RoomSettings.SessionStateKey] = RoomSettings.SessionStatePreparing,
            [RoomSettings.RoundWarmupTokenKey] = roundToken,
            [RoomSettings.RoundWarmupStartedAtKey] = PhotonNetwork.Time,
            [RoomSettings.StartTimeKey] = -1d,
            [RoomSettings.RoundEndUtcMsKey] = -1d,
            [RoomSettings.CrazyEnemiesActiveKey] = false,
            [RoomSettings.FogOfWarActiveKey] = false,
            [RoomSettings.PirateBaseActiveKey] = false,
            [RoomSettings.AsteroidShowerActiveKey] = false,
            [LoneShipModeStartTimeKey] = -1d,
            [GameTimer.EvacuationPauseUntilKey] = -1d,
            [GameTimer.EvacuationPauseRemainingKey] = -1f,
            [RoomSettings.GadgetChargesStateKey] = string.Empty,
            [RoomSettings.RepairBayOccupancyStateKey] = string.Empty,
            [RoomSettings.SpaceFactoryStateKey] = string.Empty,
            [RoomSettings.SpaceFactoryOccupancyStateKey] = string.Empty,
            [RoomSettings.ScienceStationOccupancyStateKey] = string.Empty,
            [RoomSettings.ArtifactAsteroidsStateKey] = string.Empty,
            [RoomSettings.RoundResultsKey] = string.Empty,
            [RoomSettings.FinishedRoundResultsKey] = string.Empty,
            [RoomSettings.RoundEndReasonKey] = string.Empty,
            [FireNebulaLayoutKey] = string.Empty,
            [ToxicNebulaLayoutKey] = string.Empty,
            [CloudLayoutKey] = string.Empty,
            [CloudDirectionKey] = string.Empty,
            [RepairBayLayoutKey] = string.Empty,
            [SpaceFactoryLayoutKey] = string.Empty,
            [ScienceStationLayoutKey] = string.Empty,
            [ArtifactAsteroidLayoutKey] = string.Empty
        };
    }

    static Hashtable BuildRoundStartProperties()
    {
        double roundStartUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return new Hashtable
        {
            ["gameStarted"] = true,
            [RoomSettings.StartTimeKey] = PhotonNetwork.Time,
            [RoomSettings.RoundEndUtcMsKey] = roundStartUtcMs + (RoomSettings.GetRoundDuration() * 1000d),
            [RoomSettings.SessionStateKey] = RoomSettings.SessionStateInPlay,
            [RoomSettings.CrazyEnemiesActiveKey] = RoomSettings.ShouldMapEffectActivate(RoomSettings.CrazyEnemiesModeKey, RoomSettings.CrazyEnemiesStartUtcMsKey, roundStartUtcMs),
            [RoomSettings.FogOfWarActiveKey] = RoomSettings.ShouldMapEffectActivate(RoomSettings.FogOfWarModeKey, RoomSettings.FogOfWarStartUtcMsKey, roundStartUtcMs),
            [RoomSettings.PirateBaseActiveKey] = RoomSettings.ShouldMapEffectActivate(RoomSettings.PirateBaseModeKey, RoomSettings.PirateBaseStartUtcMsKey, roundStartUtcMs),
            [RoomSettings.AsteroidShowerActiveKey] = RoomSettings.ShouldMapEffectActivate(RoomSettings.AsteroidShowerModeKey, RoomSettings.AsteroidShowerStartUtcMsKey, roundStartUtcMs),
            [LoneShipModeStartTimeKey] = -1d,
            [GameTimer.EvacuationPauseUntilKey] = -1d,
            [GameTimer.EvacuationPauseRemainingKey] = -1f,
            [RoomSettings.GadgetChargesStateKey] = string.Empty,
            [RoomSettings.RepairBayOccupancyStateKey] = string.Empty,
            [RoomSettings.SpaceFactoryStateKey] = string.Empty,
            [RoomSettings.SpaceFactoryOccupancyStateKey] = string.Empty,
            [RoomSettings.ScienceStationOccupancyStateKey] = string.Empty,
            [RoomSettings.ArtifactAsteroidsStateKey] = string.Empty,
            [RoomSettings.RoundResultsKey] = string.Empty,
            [RoomSettings.FinishedRoundResultsKey] = string.Empty,
            [RoomSettings.RoundEndReasonKey] = string.Empty,
            [FireNebulaLayoutKey] = string.Empty,
            [ToxicNebulaLayoutKey] = string.Empty,
            [CloudLayoutKey] = string.Empty,
            [CloudDirectionKey] = string.Empty,
            [RepairBayLayoutKey] = string.Empty,
            [SpaceFactoryLayoutKey] = string.Empty,
            [ScienceStationLayoutKey] = string.Empty,
            [ArtifactAsteroidLayoutKey] = string.Empty
        };
    }

    static bool IsRoomGameStarted()
    {
        return PhotonNetwork.CurrentRoom != null &&
               PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
               value is bool started &&
               started;
    }

    static void MarkLocalWarmupPending(string roundToken)
    {
        if (PhotonNetwork.LocalPlayer == null || string.IsNullOrWhiteSpace(roundToken))
            return;

        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(RoomSettings.RoundWarmupReadyPlayerKey, out object readyValue) &&
            readyValue is string readyToken &&
            string.IsNullOrEmpty(readyToken))
        {
            return;
        }

        Hashtable props = new Hashtable
        {
            [RoomSettings.RoundWarmupReadyPlayerKey] = string.Empty
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    static void MarkLocalWarmupReady(string roundToken)
    {
        if (instance != null && string.Equals(instance.readyMarkedRoundToken, roundToken, StringComparison.Ordinal))
            return;

        if (!TryGetCurrentWarmupToken(out string currentToken) ||
            !string.Equals(currentToken, roundToken, StringComparison.Ordinal) ||
            PhotonNetwork.LocalPlayer == null)
        {
            return;
        }

        Hashtable props = new Hashtable
        {
            [RoomSettings.RoundWarmupReadyPlayerKey] = roundToken
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        if (instance != null)
            instance.readyMarkedRoundToken = roundToken;
    }

    void TryCommitPreparedRoundStart(string roundToken)
    {
        if (string.IsNullOrWhiteSpace(roundToken) ||
            string.Equals(committedRoundToken, roundToken, StringComparison.Ordinal) ||
            !string.Equals(warmedRoundToken, roundToken, StringComparison.Ordinal))
        {
            return;
        }

        if (!HasWarmupWaitElapsed(out bool timedOut) && !AreAllPlayersReady(roundToken))
            return;

        committedRoundToken = roundToken;
        if (PhotonNetwork.CurrentRoom != null)
            PhotonNetwork.CurrentRoom.IsOpen = true;

        if (timedOut)
            Debug.LogWarning("RoundWarmupService: starting round after warmup timeout; at least one player did not report ready.");

        if (PhotonNetwork.CurrentRoom == null)
            return;

        PhotonNetwork.CurrentRoom.SetCustomProperties(BuildRoundStartProperties());
        RoundResultsTracker.ResetForCurrentRoom();
    }

    static bool HasWarmupWaitElapsed(out bool timedOut)
    {
        timedOut = false;
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        double startedAt = -1d;
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.RoundWarmupStartedAtKey, out object value))
        {
            if (value is double doubleValue)
                startedAt = doubleValue;
            else if (value is float floatValue)
                startedAt = floatValue;
            else if (value is int intValue)
                startedAt = intValue;
        }

        if (startedAt < 0d)
            return false;

        timedOut = PhotonNetwork.Time - startedAt >= MaxNetworkWarmupWaitSeconds;
        return timedOut;
    }

    static bool AreAllPlayersReady(string roundToken)
    {
        Player[] players = PhotonNetwork.PlayerList;
        if (players == null || players.Length == 0)
            return true;

        for (int i = 0; i < players.Length; i++)
        {
            Player player = players[i];
            if (player == null)
                continue;

            if (!player.CustomProperties.TryGetValue(RoomSettings.RoundWarmupReadyPlayerKey, out object value) ||
                !(value is string readyToken) ||
                !string.Equals(readyToken, roundToken, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    static void PrewarmEnemyDefinition(EnemyBotDefinition definition)
    {
        if (definition == null)
            return;

        PrewarmSpriteTexture(definition.GetVisualSprite());

        if (!string.IsNullOrWhiteSpace(definition.AnimationResourcePath))
            PrewarmSprites(EnemySpriteFrameAnimator.PrewarmFrames(definition.AnimationResourcePath));

        if (definition.Wreck != null)
            PrewarmSpriteTexture(definition.Wreck.GetVisualSprite());

        if (definition.Explosion != null)
            PrewarmSprites(definition.Explosion.GetVisualFrames());
    }

    static void PrewarmEnemySpecialAssets()
    {
        EnemyContainerShipBehavior.PrewarmCargoSprites();
        EnemyMothershipBehavior.PrewarmTurretAssets();

        RescueShipBeamVfx.Prewarm();
        PirateBaseCollectionBeamVfx.Prewarm();
        GravitySquidTetherVfx.Prewarm();
        HunterLanceBeamVfx.Prewarm();
        SpaceAnimalDeathVfx.Prewarm();
        PirateBaseLaunchVfx.Prewarm();
    }

    static void PrewarmPhotonPrefabs()
    {
        for (int i = 0; i < PhotonPrefabResourcePaths.Length; i++)
            Resources.Load<GameObject>(PhotonPrefabResourcePaths[i]);
    }

    static void PrewarmVfx()
    {
        BulletImpactVfx.Prewarm();
        ShieldHitVfx.Prewarm();
        HpHitSparksVfx.Prewarm();
        PlayerShipExplosionVfx.Prewarm();
        EnemyDeathBoomVfx.Prewarm();
        AsteroidSplitVfx.Prewarm();
        BatteringImpactVfx.Prewarm();
        EnemySpawnTeleportVfx.Prewarm();
        TractorBeamVfx.Prewarm();
        MagneticBeamVfx.Prewarm();
        SpaceMineExplosionVfx.Prewarm();
        SpaceBombExplosionVfx.Prewarm();
        AsteroidShowerStrikeVfx.Prewarm();
    }

    static void PrewarmResourceSprites(string[] resourcePaths)
    {
        if (resourcePaths == null)
            return;

        for (int i = 0; i < resourcePaths.Length; i++)
            PrewarmResourceSprite(resourcePaths[i]);
    }

    static void PrewarmResourceSprite(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return;

        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
        {
            PrewarmSpriteTexture(sprite);
            return;
        }

        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
        if (sprites != null && sprites.Length > 0)
        {
            PrewarmSprites(sprites);
            return;
        }

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        PrewarmTexture(texture);
    }

    static void PrewarmSprites(Sprite[] sprites)
    {
        if (sprites == null)
            return;

        for (int i = 0; i < sprites.Length; i++)
            PrewarmSpriteTexture(sprites[i]);
    }

    static void PrewarmSpriteTexture(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        sprite.texture.GetNativeTexturePtr();
    }

    static void PrewarmTexture(Texture2D texture)
    {
        if (texture == null)
            return;

        texture.GetNativeTexturePtr();
    }
}
