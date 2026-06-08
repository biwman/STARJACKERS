using System;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;

public sealed class ArrowRacePlotController : MonoBehaviour
{
    const string PersistentHintOwnerKey = "arrow_racing_plot";
    const float ScanInterval = 0.12f;
    const float BeaconActivationRadius = 2.2f;
    const float CheckpointRadius = 2.35f;
    const float MarkerWorldSize = 3.8f;
    const float PlotStartMatchTolerance = 0.001f;

    static ArrowRacePlotController instance;
    static Sprite ringSprite;
    static Sprite glowDotSprite;
    static bool localFinalRunObjectivesComplete;

    readonly List<Vector2> route = new List<Vector2>(16);

    GameObject markerRoot;
    GameObject beaconObject;
    GameObject checkpointObject;
    TMP_Text beaconLabel;
    TMP_Text checkpointLabel;
    ChallengeMode activeChallenge = ChallengeMode.None;
    ArrowLicenseStage displayedStage = (ArrowLicenseStage)(-1);
    double handledStartTime = double.MinValue;
    float nextScanTime;
    float challengeStartTime;
    float challengeTimeLimit;
    int checkpointIndex;
    bool finalRunBeginInProgress;
    bool finalRunAwaitingExtraction;

    enum ChallengeMode
    {
        None,
        Qualifier,
        PartSprint,
        TimeTrial,
        GhostRace,
        FinalRun
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureExists();
    }

    public static void EnsureExists()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("ArrowRacePlotController");
        instance = root.AddComponent<ArrowRacePlotController>();
        DontDestroyOnLoad(root);
    }

    public static bool IsLocalFinalRunReadyForExtraction()
    {
        return localFinalRunObjectivesComplete;
    }

    public static void ClearLocalFinalRunState()
    {
        localFinalRunObjectivesComplete = false;
        if (instance != null)
            instance.ResetChallenge();
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

    void OnDestroy()
    {
        RoundAnnouncementUI.ClearPersistentHint(PersistentHintOwnerKey);
        if (markerRoot != null)
            Destroy(markerRoot);

        if (instance == this)
            instance = null;
    }

    void Update()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
        {
            ResetLocalState();
            return;
        }

        float now = Time.unscaledTime;
        if (now < nextScanTime)
            return;

        nextScanTime = now + ScanInterval;
        TickLifecycle();
    }

    void TickLifecycle()
    {
        if (!IsRoundStarted(out double currentStartTime) ||
            !ShipUnlockPlotCoordinator.IsActivePlot(ShipUnlockPlotType.Arrow))
        {
            ResetLocalState();
            return;
        }

        if (currentStartTime != handledStartTime)
        {
            handledStartTime = currentStartTime;
            ResetChallenge();
        }

        if (!PlayerProfileService.HasInstance || !PlayerProfileService.Instance.IsInitialized)
            return;

        ArrowLicenseProgressData progress = PlayerProfileService.Instance.GetArrowLicenseProgress();
        ArrowLicenseStage stage = (ArrowLicenseStage)Mathf.Clamp(progress.Stage, (int)ArrowLicenseStage.Locked, (int)ArrowLicenseStage.Complete);
        if (stage == ArrowLicenseStage.Complete)
        {
            ResetLocalState();
            return;
        }

        PlayerHealth player = ResolveLocalPlayer();
        if (player == null || player.IsWreck || player.IsEvacuationAnimating)
            return;

        if (activeChallenge != ChallengeMode.None)
        {
            TickChallenge(player, stage);
            return;
        }

        EnsureBeacon(stage, player.transform.position);
        RoundAnnouncementUI.SetPersistentHint(PersistentHintOwnerKey, BuildBeaconHint(stage));
        if (Vector2.Distance(player.transform.position, beaconObject.transform.position) <= BeaconActivationRadius)
            StartChallengeForStage(stage, player);
    }

    void StartChallengeForStage(ArrowLicenseStage stage, PlayerHealth player)
    {
        switch (stage)
        {
            case ArrowLicenseStage.Locked:
            case ArrowLicenseStage.Qualifying:
                BeginCheckpointChallenge(ChallengeMode.Qualifier, player.transform.position, 7, 65f, "Arrow qualifier started.");
                break;
            case ArrowLicenseStage.PartsRequired:
                BeginCheckpointChallenge(ChallengeMode.PartSprint, player.transform.position, 7, 70f, "Arrow tuning sprint started.");
                break;
            case ArrowLicenseStage.TimeTrialRequired:
                BeginCheckpointChallenge(ChallengeMode.TimeTrial, player.transform.position, 12, 110f, "Arrow time trial started.");
                break;
            case ArrowLicenseStage.GhostRaceRequired:
                BeginCheckpointChallenge(ChallengeMode.GhostRace, player.transform.position, 14, 105f, "Ghost racer The Needle is on track.");
                break;
            case ArrowLicenseStage.FinalRunReady:
                BeginFinalRun(player);
                break;
        }
    }

    async void BeginFinalRun(PlayerHealth player)
    {
        if (finalRunBeginInProgress || player == null || player.photonView == null)
            return;

        finalRunBeginInProgress = true;
        int originalSkin = RoomSettings.GetPlayerShipSkin(PhotonNetwork.LocalPlayer, ShipCatalog.ExplorerBasicSkinIndex);
        bool started = false;
        try
        {
            started = await PlayerProfileService.Instance.BeginArrowFinalRunAsync(originalSkin);
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to begin Arrow final run: " + ex);
        }
        finally
        {
            finalRunBeginInProgress = false;
        }

        if (!started)
            return;

        Vector2 launch = ResolveSafeLaunchPosition(player.transform.position);
        player.photonView.RPC(
            nameof(PlayerHealth.BeginAvengerShipOverrideRpc),
            RpcTarget.All,
            ShipCatalog.ArrowSmoothSkinIndex,
            launch.x,
            launch.y,
            player.transform.eulerAngles.z);

        BeginCheckpointChallenge(ChallengeMode.FinalRun, launch, 10, 180f, "Final Arrow Run started.");
    }

    void BeginCheckpointChallenge(ChallengeMode mode, Vector2 origin, int checkpointCount, float timeLimit, string announcement)
    {
        DestroyBeacon();
        activeChallenge = mode;
        finalRunAwaitingExtraction = false;
        if (mode != ChallengeMode.FinalRun)
            localFinalRunObjectivesComplete = false;

        challengeStartTime = Time.unscaledTime;
        challengeTimeLimit = Mathf.Max(15f, timeLimit);
        checkpointIndex = 0;
        route.Clear();
        BuildRoute(origin, checkpointCount, mode, route);
        SpawnCheckpoint();
        RoundAnnouncementUI.Show(announcement, 2.8f);
    }

    void TickChallenge(PlayerHealth player, ArrowLicenseStage stage)
    {
        if (activeChallenge == ChallengeMode.FinalRun && finalRunAwaitingExtraction)
        {
            RoundAnnouncementUI.SetPersistentHint(PersistentHintOwnerKey, "Final Arrow Run complete: extract alive.");
            return;
        }

        float elapsed = Time.unscaledTime - challengeStartTime;
        if (elapsed > challengeTimeLimit)
        {
            FailChallenge("Arrow racing attempt timed out.");
            return;
        }

        if (checkpointObject == null || checkpointIndex >= route.Count)
        {
            CompleteChallenge(elapsed);
            return;
        }

        RoundAnnouncementUI.SetPersistentHint(PersistentHintOwnerKey, BuildChallengeHint(elapsed));
        if (Vector2.Distance(player.transform.position, checkpointObject.transform.position) > CheckpointRadius)
            return;

        checkpointIndex++;
        if (checkpointIndex >= route.Count)
            CompleteChallenge(elapsed);
        else
            SpawnCheckpoint();
    }

    async void CompleteChallenge(float elapsed)
    {
        DestroyCheckpoint();

        switch (activeChallenge)
        {
            case ChallengeMode.Qualifier:
                await PlayerProfileService.Instance.RecordArrowQualifierTrialAsync();
                RoundAnnouncementUI.Show("Arrow qualifier complete.", 3f);
                ResetChallenge();
                break;
            case ChallengeMode.PartSprint:
                string partId = await PlayerProfileService.Instance.RecordArrowRacingPartAsync();
                RoundAnnouncementUI.Show("Arrow part secured: " + FormatArrowPartName(partId), 3f);
                ResetChallenge();
                break;
            case ChallengeMode.TimeTrial:
                ArrowTimeTrialRank rank = await PlayerProfileService.Instance.RecordArrowTimeTrialAsync(elapsed);
                RoundAnnouncementUI.Show("Arrow time trial rank: " + FormatTimeTrialRank(rank), 3f);
                ResetChallenge();
                break;
            case ChallengeMode.GhostRace:
                if (elapsed <= 102f)
                {
                    await PlayerProfileService.Instance.RecordArrowGhostRaceWonAsync();
                    RoundAnnouncementUI.Show("The Needle defeated. Final Arrow Run ready.", 3.2f);
                }
                else
                {
                    RoundAnnouncementUI.Show("The Needle stayed ahead.", 2.8f);
                }

                ResetChallenge();
                break;
            case ChallengeMode.FinalRun:
                localFinalRunObjectivesComplete = true;
                finalRunAwaitingExtraction = true;
                RoundAnnouncementUI.Show("Telemetry collected. Extract alive.", 3f);
                break;
        }
    }

    async void FailChallenge(string message)
    {
        if (activeChallenge == ChallengeMode.FinalRun)
        {
            try
            {
                await PlayerProfileService.Instance.FailArrowFinalRunAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to fail Arrow final run: " + ex);
            }
        }

        RoundAnnouncementUI.Show(message, 2.8f);
        ResetChallenge();
    }

    void EnsureBeacon(ArrowLicenseStage stage, Vector2 playerPosition)
    {
        EnsureMarkerRoot();
        if (beaconObject == null || displayedStage != stage)
        {
            DestroyBeacon();
            beaconObject = CreateMarker("ArrowRaceBeacon", GetBeaconColor(stage), MarkerWorldSize, out beaconLabel);
            displayedStage = stage;
        }

        beaconObject.transform.position = ResolveBeaconPosition(stage, playerPosition);
        if (beaconLabel != null)
            beaconLabel.text = GetBeaconLabel(stage);
    }

    void SpawnCheckpoint()
    {
        EnsureMarkerRoot();
        DestroyCheckpoint();
        checkpointObject = CreateMarker("ArrowRaceCheckpoint", GetChallengeColor(activeChallenge), MarkerWorldSize, out checkpointLabel);
        checkpointObject.transform.position = route[Mathf.Clamp(checkpointIndex, 0, route.Count - 1)];
        UpdateCheckpointLabel();
    }

    GameObject CreateMarker(string name, Color color, float worldSize, out TMP_Text label)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(markerRoot.transform, false);
        root.transform.localScale = Vector3.one * worldSize;

        SpriteRenderer halo = CreateRingLayer(root.transform, "IridescentHalo", color, 1.18f, GameVisualTheme.PlayerSortingOrder + 11);
        SpriteRenderer primary = CreateRingLayer(root.transform, "IridescentRing", color, 1f, GameVisualTheme.PlayerSortingOrder + 12);
        SpriteRenderer inner = CreateRingLayer(root.transform, "IridescentInnerRing", color, 0.72f, GameVisualTheme.PlayerSortingOrder + 13);
        SpriteRenderer[] glints = CreateMarkerGlints(root.transform, color, GameVisualTheme.PlayerSortingOrder + 14);
        ArrowRaceMarkerVisual visual = root.AddComponent<ArrowRaceMarkerVisual>();
        visual.Configure(primary, halo, inner, glints, color);

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(root.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 0f, -0.02f);
        labelObject.transform.localScale = Vector3.one / Mathf.Max(0.01f, worldSize);

        label = labelObject.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 3.6f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.rectTransform.sizeDelta = new Vector2(7f, 3f);
        MeshRenderer labelRenderer = label.GetComponent<MeshRenderer>();
        if (labelRenderer != null)
        {
            labelRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
            labelRenderer.sortingOrder = GameVisualTheme.PlayerSortingOrder + 16;
        }

        return root;
    }

    SpriteRenderer CreateRingLayer(Transform parent, string name, Color color, float localScale, int sortingOrder)
    {
        GameObject layer = new GameObject(name);
        layer.transform.SetParent(parent, false);
        layer.transform.localPosition = Vector3.zero;
        layer.transform.localRotation = Quaternion.identity;
        layer.transform.localScale = Vector3.one * localScale;

        SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
        renderer.sprite = GetRingSprite();
        renderer.color = color;
        renderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    SpriteRenderer[] CreateMarkerGlints(Transform parent, Color color, int sortingOrder)
    {
        const int GlintCount = 7;
        SpriteRenderer[] glints = new SpriteRenderer[GlintCount];
        for (int i = 0; i < GlintCount; i++)
        {
            GameObject glint = new GameObject("IridescentGlint" + i);
            glint.transform.SetParent(parent, false);
            glint.transform.localScale = Vector3.one * (i % 2 == 0 ? 0.082f : 0.058f);

            SpriteRenderer renderer = glint.AddComponent<SpriteRenderer>();
            renderer.sprite = GetGlowDotSprite();
            renderer.color = color;
            renderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
            renderer.sortingOrder = sortingOrder;
            glints[i] = renderer;
        }

        return glints;
    }

    void UpdateCheckpointLabel()
    {
        if (checkpointLabel == null)
            return;

        checkpointLabel.text = activeChallenge == ChallengeMode.FinalRun
            ? "DATA\n" + (checkpointIndex + 1) + "/" + route.Count
            : (checkpointIndex + 1) + "/" + route.Count;
    }

    void BuildRoute(Vector2 origin, int count, ChallengeMode mode, List<Vector2> output)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = Mathf.Max(10f, mapSize.x * 0.5f - 5f);
        float halfY = Mathf.Max(10f, mapSize.y * 0.5f - 5f);
        float baseRadius = Mathf.Min(halfX, halfY) * (mode == ChallengeMode.FinalRun ? 0.62f : 0.46f);
        float angleOffset = Mathf.Repeat((float)PhotonNetwork.Time * 0.37f + (int)mode * 0.71f, Mathf.PI * 2f);

        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 0f : i / (float)(count - 1);
            float angle = angleOffset + i * 1.42f;
            float radius = Mathf.Lerp(baseRadius * 0.55f, baseRadius, (i % 3) / 2f);
            if (mode == ChallengeMode.GhostRace)
                radius *= 1.08f;

            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            offset += new Vector2(Mathf.Cos(angle + Mathf.PI * 0.5f), Mathf.Sin(angle + Mathf.PI * 0.5f)) * Mathf.Sin(t * Mathf.PI * 2f) * 4.5f;
            Vector2 candidate = origin + offset;
            output.Add(new Vector2(
                Mathf.Clamp(candidate.x, -halfX, halfX),
                Mathf.Clamp(candidate.y, -halfY, halfY)));
        }
    }

    Vector2 ResolveBeaconPosition(ArrowLicenseStage stage, Vector2 playerPosition)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = Mathf.Max(8f, mapSize.x * 0.5f - 6f);
        float halfY = Mathf.Max(8f, mapSize.y * 0.5f - 6f);
        float angle = 0.85f + (int)stage * 0.72f;
        Vector2 candidate = playerPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 10f;
        return new Vector2(Mathf.Clamp(candidate.x, -halfX, halfX), Mathf.Clamp(candidate.y, -halfY, halfY));
    }

    Vector2 ResolveSafeLaunchPosition(Vector2 playerPosition)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = Mathf.Max(8f, mapSize.x * 0.5f - 6f);
        float halfY = Mathf.Max(8f, mapSize.y * 0.5f - 6f);
        return new Vector2(Mathf.Clamp(playerPosition.x, -halfX, halfX), Mathf.Clamp(playerPosition.y, -halfY, halfY));
    }

    PlayerHealth ResolveLocalPlayer()
    {
        if (PhotonNetwork.LocalPlayer != null && PhotonNetwork.LocalPlayer.TagObject is GameObject taggedObject)
        {
            PlayerHealth taggedHealth = taggedObject != null ? taggedObject.GetComponent<PlayerHealth>() : null;
            if (taggedHealth != null && taggedHealth.photonView != null && taggedHealth.photonView.IsMine)
                return taggedHealth;
        }

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth player = players[i];
            if (player != null && player.photonView != null && player.photonView.IsMine)
                return player;
        }

        return null;
    }

    string BuildBeaconHint(ArrowLicenseStage stage)
    {
        switch (stage)
        {
            case ArrowLicenseStage.Locked:
            case ArrowLicenseStage.Qualifying:
                return "Fly into Arrow Race Beacon: qualifier trial.";
            case ArrowLicenseStage.PartsRequired:
                return "Fly into Arrow Race Beacon: tuning sprint.";
            case ArrowLicenseStage.TimeTrialRequired:
                return "Fly into Arrow Race Beacon: time trial.";
            case ArrowLicenseStage.GhostRaceRequired:
                return "Fly into Arrow Race Beacon: ghost race.";
            case ArrowLicenseStage.FinalRunReady:
                return "Fly into Arrow Race Beacon: final Arrow Run.";
            default:
                return string.Empty;
        }
    }

    string BuildChallengeHint(float elapsed)
    {
        float remaining = Mathf.Max(0f, challengeTimeLimit - elapsed);
        return "Arrow racing: checkpoint " + Mathf.Min(checkpointIndex + 1, route.Count) + "/" + route.Count + " - " + Mathf.CeilToInt(remaining) + "s";
    }

    string GetBeaconLabel(ArrowLicenseStage stage)
    {
        switch (stage)
        {
            case ArrowLicenseStage.PartsRequired: return "ARROW\nPARTS";
            case ArrowLicenseStage.TimeTrialRequired: return "ARROW\nTIME";
            case ArrowLicenseStage.GhostRaceRequired: return "GHOST\nRACE";
            case ArrowLicenseStage.FinalRunReady: return "FINAL\nRUN";
            default: return "ARROW\nQUAL";
        }
    }

    Color GetBeaconColor(ArrowLicenseStage stage)
    {
        switch (stage)
        {
            case ArrowLicenseStage.PartsRequired: return new Color(0.25f, 0.9f, 0.72f, 0.86f);
            case ArrowLicenseStage.TimeTrialRequired: return new Color(1f, 0.8f, 0.24f, 0.9f);
            case ArrowLicenseStage.GhostRaceRequired: return new Color(0.78f, 0.55f, 1f, 0.88f);
            case ArrowLicenseStage.FinalRunReady: return new Color(1f, 0.36f, 0.24f, 0.92f);
            default: return new Color(0.35f, 0.75f, 1f, 0.86f);
        }
    }

    Color GetChallengeColor(ChallengeMode mode)
    {
        switch (mode)
        {
            case ChallengeMode.PartSprint: return new Color(0.18f, 1f, 0.72f, 0.9f);
            case ChallengeMode.TimeTrial: return new Color(1f, 0.82f, 0.22f, 0.92f);
            case ChallengeMode.GhostRace: return new Color(0.78f, 0.54f, 1f, 0.9f);
            case ChallengeMode.FinalRun: return new Color(1f, 0.33f, 0.22f, 0.94f);
            default: return new Color(0.32f, 0.78f, 1f, 0.9f);
        }
    }

    string FormatArrowPartName(string partId)
    {
        switch (partId)
        {
            case PlayerProfileService.ArrowIonNozzlePartId: return "Ion Nozzle";
            case PlayerProfileService.ArrowGyroStabilizerPartId: return "Gyro Stabilizer";
            case PlayerProfileService.ArrowRaceTransponderPartId: return "Race Transponder";
            default: return "unknown";
        }
    }

    string FormatTimeTrialRank(ArrowTimeTrialRank rank)
    {
        return rank == ArrowTimeTrialRank.None ? "-" : rank.ToString();
    }

    void EnsureMarkerRoot()
    {
        if (markerRoot != null)
            return;

        markerRoot = new GameObject("ArrowRacePlotMarkers");
        DontDestroyOnLoad(markerRoot);
    }

    void DestroyBeacon()
    {
        if (beaconObject != null)
            Destroy(beaconObject);

        beaconObject = null;
        beaconLabel = null;
        displayedStage = (ArrowLicenseStage)(-1);
    }

    void DestroyCheckpoint()
    {
        if (checkpointObject != null)
            Destroy(checkpointObject);

        checkpointObject = null;
        checkpointLabel = null;
    }

    void ResetChallenge()
    {
        activeChallenge = ChallengeMode.None;
        finalRunBeginInProgress = false;
        finalRunAwaitingExtraction = false;
        checkpointIndex = 0;
        route.Clear();
        DestroyCheckpoint();
        RoundAnnouncementUI.ClearPersistentHint(PersistentHintOwnerKey);
    }

    void ResetLocalState()
    {
        handledStartTime = double.MinValue;
        localFinalRunObjectivesComplete = false;
        ResetChallenge();
        DestroyBeacon();
    }

    static Sprite GetRingSprite()
    {
        if (ringSprite != null)
            return ringSprite;

        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outer = size * 0.43f;
        float inner = size * 0.29f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.SmoothStep(0f, 1f, outer - distance) * Mathf.SmoothStep(0f, 1f, distance - inner);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        ringSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return ringSprite;
    }

    static Sprite GetGlowDotSprite()
    {
        if (glowDotSprite != null)
            return glowDotSprite;

        const int size = 48;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.46f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float normalized = Mathf.Clamp01(distance / radius);
                float alpha = Mathf.Pow(1f - normalized, 2.4f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        glowDotSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return glowDotSprite;
    }

    static bool IsRoundStarted(out double currentStartTime)
    {
        currentStartTime = 0d;
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object startedValue) ||
            !(startedValue is bool started) ||
            !started)
        {
            return false;
        }

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.StartTimeKey, out object startValue))
            TryConvertToDouble(startValue, out currentStartTime);

        return true;
    }

    static bool TryConvertToDouble(object value, out double result)
    {
        switch (value)
        {
            case double d:
                result = d;
                return true;
            case float f:
                result = f;
                return true;
            case int i:
                result = i;
                return true;
            case long l:
                result = l;
                return true;
            default:
                result = 0d;
                return false;
        }
    }
}

sealed class ArrowRaceMarkerVisual : MonoBehaviour
{
    const float GlintOrbitRadius = 0.43f;

    SpriteRenderer primaryRing;
    SpriteRenderer haloRing;
    SpriteRenderer innerRing;
    SpriteRenderer[] glints;
    Vector3 primaryBaseScale;
    Vector3 haloBaseScale;
    Vector3 innerBaseScale;
    Vector3[] glintBaseScales;
    float[] glintAngles;
    float baseHue;
    float baseSaturation;
    float baseValue;
    float phase;
    Color baseColor;

    public void Configure(SpriteRenderer primary, SpriteRenderer halo, SpriteRenderer inner, SpriteRenderer[] orbitGlints, Color color)
    {
        primaryRing = primary;
        haloRing = halo;
        innerRing = inner;
        glints = orbitGlints ?? Array.Empty<SpriteRenderer>();
        baseColor = color;
        Color.RGBToHSV(color, out baseHue, out baseSaturation, out baseValue);
        baseSaturation = Mathf.Clamp(baseSaturation, 0.48f, 0.86f);
        baseValue = Mathf.Clamp(baseValue, 0.8f, 1f);
        phase = UnityEngine.Random.value * 20f;

        primaryBaseScale = primaryRing != null ? primaryRing.transform.localScale : Vector3.one;
        haloBaseScale = haloRing != null ? haloRing.transform.localScale : Vector3.one;
        innerBaseScale = innerRing != null ? innerRing.transform.localScale : Vector3.one;
        glintBaseScales = new Vector3[glints.Length];
        glintAngles = new float[glints.Length];
        for (int i = 0; i < glints.Length; i++)
        {
            glintBaseScales[i] = glints[i] != null ? glints[i].transform.localScale : Vector3.one * 0.06f;
            glintAngles[i] = (Mathf.PI * 2f * i / Mathf.Max(1, glints.Length)) + UnityEngine.Random.Range(-0.22f, 0.22f);
        }
    }

    void Update()
    {
        float t = Time.unscaledTime + phase;
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4.4f);
        float glimmer = 0.5f + 0.5f * Mathf.Sin(t * 7.2f + 1.1f);

        ApplyRing(primaryRing, primaryBaseScale, t, 0f, 0.74f, 0.96f, Mathf.Lerp(0.78f, 0.98f, pulse), 0.018f, 18f);
        ApplyRing(haloRing, haloBaseScale, t, 0.035f, 0.28f, 0.98f, Mathf.Lerp(0.14f, 0.3f, glimmer), 0.055f, -10f);
        ApplyRing(innerRing, innerBaseScale, t, -0.025f, 0.48f, 0.98f, Mathf.Lerp(0.42f, 0.68f, 1f - pulse), 0.028f, 30f);
        UpdateGlints(t);
    }

    void ApplyRing(SpriteRenderer renderer, Vector3 baseScale, float t, float hueOffset, float saturation, float value, float alpha, float scalePulse, float rotationSpeed)
    {
        if (renderer == null)
            return;

        renderer.color = BuildOpalescentColor(t, hueOffset, saturation, value, alpha);
        renderer.transform.localRotation = Quaternion.Euler(0f, 0f, t * rotationSpeed);
        float scale = 1f + Mathf.Sin(t * 3.2f + hueOffset * 9f) * scalePulse;
        renderer.transform.localScale = baseScale * scale;
    }

    void UpdateGlints(float t)
    {
        for (int i = 0; i < glints.Length; i++)
        {
            SpriteRenderer glint = glints[i];
            if (glint == null)
                continue;

            float speed = 0.46f + i * 0.022f;
            float angle = glintAngles[i] + t * speed;
            float sparkle = 0.5f + 0.5f * Mathf.Sin(t * 4.8f + i * 1.37f);
            float radius = GlintOrbitRadius + Mathf.Sin(t * 1.6f + i) * 0.018f;
            glint.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, -0.035f);
            glint.transform.localScale = glintBaseScales[i] * Mathf.Lerp(0.65f, 1.16f, sparkle);
            glint.color = BuildOpalescentColor(t, i % 2 == 0 ? 0.025f : -0.018f, 0.22f, 1f, Mathf.Lerp(0.12f, 0.55f, sparkle));
        }
    }

    Color BuildOpalescentColor(float t, float hueOffset, float saturation, float value, float alpha)
    {
        float hue = Mathf.Repeat(baseHue + hueOffset + Mathf.Sin(t * 0.48f + hueOffset * 9f) * 0.014f, 1f);
        Color tinted = Color.HSVToRGB(hue, Mathf.Clamp01(Mathf.Min(baseSaturation, saturation)), Mathf.Clamp(value, baseValue * 0.92f, 1f));
        Color pearl = new Color(0.82f, 0.94f, 1f, 1f);
        Color color = Color.Lerp(tinted, pearl, hueOffset == 0f ? 0.08f : 0.22f);
        color.a = Mathf.Clamp01(alpha * Mathf.Max(0.25f, baseColor.a));
        return color;
    }
}
