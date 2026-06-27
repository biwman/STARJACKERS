using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

public sealed class PerformanceDiagnosticsOverlay : MonoBehaviour
{
    const float StatsRefreshInterval = 0.5f;

    static PerformanceDiagnosticsOverlay instance;
    static int motionSnapshotsSentSinceLastSample;

    readonly StringBuilder builder = new StringBuilder(768);

    Canvas canvas;
    TextMeshProUGUI text;
    ProfilerRecorder gcAllocatedRecorder;

    float nextStatsRefreshTime;
    float lastStatsRefreshTime;
    float lastPhotonSampleTime;
    float accumulatedFrameTime;
    long lastPhotonOutgoingMessages;
    long lastPhotonIncomingMessages;
    long lastPhotonTotalMessages;
    bool hasPhotonSample;
    bool trafficStatsEnabledByOverlay;

    int framesSinceLastSample;
    int currentFps;
    float currentFrameMs;
    int obstacleChunks;
    int movingObjects;
    int treasures;
    int droppedCargo;
    int shipWrecks;
    int bullets;
    int enemyBots;
    int photonViews;
    int rigidbodies2D;
    int motionSnapshotsPerSecond;
    int photonOutgoingPerSecond;
    int photonIncomingPerSecond;
    int photonTotalPerSecond;
    long gcAllocatedBytes;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("PerformanceDiagnosticsOverlay");
        instance = root.AddComponent<PerformanceDiagnosticsOverlay>();
        DontDestroyOnLoad(root);
    }

    public static void RecordMotionSnapshotSent()
    {
        motionSnapshotsSentSinceLastSample++;
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
        CreateUi();
        ApplyVisibility();
    }

    void OnDestroy()
    {
        if (gcAllocatedRecorder.Valid)
            gcAllocatedRecorder.Dispose();

        SetPhotonTrafficStatsEnabled(false);

        if (instance == this)
            instance = null;
    }

    void Update()
    {
        ApplyVisibility();
        bool visible = canvas != null && canvas.enabled;
        bool fpsEnabled = visible && RoomSettings.IsFpsCounterEnabled();
        bool gcEnabled = visible && RoomSettings.IsDiagnosticsGcEnabled();
        bool sceneCountsEnabled = visible && RoomSettings.IsDiagnosticsSceneCountsEnabled();
        bool networkEnabled = visible && RoomSettings.IsDiagnosticsNetworkEnabled();

        SetGcRecorderEnabled(gcEnabled);
        SetPhotonTrafficStatsEnabled(networkEnabled);

        if (!visible)
            return;

        if (fpsEnabled)
        {
            float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            framesSinceLastSample++;
            accumulatedFrameTime += deltaTime;
        }
        else
        {
            framesSinceLastSample = 0;
            accumulatedFrameTime = 0f;
        }

        if (Time.unscaledTime >= nextStatsRefreshTime)
        {
            float now = Time.unscaledTime;
            float sampleInterval = lastStatsRefreshTime > 0f
                ? Mathf.Max(0.001f, now - lastStatsRefreshTime)
                : StatsRefreshInterval;
            lastStatsRefreshTime = now;

            if (fpsEnabled)
                CollectFrameStats(sampleInterval);

            if (gcEnabled)
                CollectGcStats();
            else
                gcAllocatedBytes = 0L;

            if (sceneCountsEnabled)
                CollectSceneStats();
            else
                ClearSceneStats();

            if (networkEnabled)
                CollectNetworkStats(sampleInterval);
            else
                ClearNetworkStats();

            RenderText(fpsEnabled, gcEnabled, sceneCountsEnabled, networkEnabled);
            nextStatsRefreshTime = now + StatsRefreshInterval;
        }
    }

    void CreateUi()
    {
        GameObject canvasObject = new GameObject("DiagnosticsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = new GameObject("DiagnosticsPanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(18f, -18f);
        panelRect.sizeDelta = new Vector2(395f, 440f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.025f, 0.035f, 0.05f, 0.86f);
        panelImage.raycastTarget = false;

        GameObject textObject = new GameObject("DiagnosticsText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panelObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 14f);
        textRect.offsetMax = new Vector2(-16f, -14f);

        text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = 18f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = new Color(0.88f, 0.98f, 1f, 0.98f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        TryApplyReferenceFont();
    }

    void TryApplyReferenceFont()
    {
        if (text == null || text.font != null)
            return;

        TMP_Text reference = FindAnyObjectByType<TMP_Text>();
        if (reference == null)
            return;

        text.font = reference.font;
        text.fontSharedMaterial = reference.fontSharedMaterial;
    }

    void SetGcRecorderEnabled(bool enabled)
    {
        if (enabled)
        {
            if (!gcAllocatedRecorder.Valid)
                gcAllocatedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

            return;
        }

        if (gcAllocatedRecorder.Valid)
            gcAllocatedRecorder.Dispose();
    }

    void ApplyVisibility()
    {
        bool shouldShow = RoomSettings.IsAnyDiagnosticsOverlayEnabled();
        if (canvas != null && canvas.enabled != shouldShow)
        {
            canvas.enabled = shouldShow;
            if (shouldShow)
            {
                ResetPhotonSample();
                motionSnapshotsSentSinceLastSample = 0;
                ResetFrameStats(Time.unscaledTime);
                TryApplyReferenceFont();
            }
            else
            {
                SetGcRecorderEnabled(false);
                SetPhotonTrafficStatsEnabled(false);
            }
        }
    }

    void ResetFrameStats(float now)
    {
        framesSinceLastSample = 0;
        accumulatedFrameTime = 0f;
        lastStatsRefreshTime = now;
        nextStatsRefreshTime = now + StatsRefreshInterval;
    }

    void CollectFrameStats(float sampleInterval)
    {
        int frames = Mathf.Max(1, framesSinceLastSample);
        currentFps = Mathf.RoundToInt(framesSinceLastSample / Mathf.Max(0.001f, sampleInterval));
        currentFrameMs = (accumulatedFrameTime / frames) * 1000f;
        framesSinceLastSample = 0;
        accumulatedFrameTime = 0f;
    }

    void CollectGcStats()
    {
        gcAllocatedBytes = gcAllocatedRecorder.Valid ? gcAllocatedRecorder.LastValue : 0L;
    }

    void CollectSceneStats()
    {
        obstacleChunks = CountActive<ObstacleChunk>();
        movingObjects = CountActive<MovingSpaceObject>();
        treasures = CountActive<Treasure>();
        droppedCargo = CountActive<DroppedCargoCrate>();
        shipWrecks = CountActive<ShipWreck>();
        bullets = CountActive<Bullet>();
        enemyBots = CountActive<EnemyBot>();
        photonViews = CountActive<PhotonView>();
        rigidbodies2D = CountActive<Rigidbody2D>();
    }

    void ClearSceneStats()
    {
        obstacleChunks = 0;
        movingObjects = 0;
        treasures = 0;
        droppedCargo = 0;
        shipWrecks = 0;
        bullets = 0;
        enemyBots = 0;
        photonViews = 0;
        rigidbodies2D = 0;
    }

    void CollectNetworkStats(float sampleInterval)
    {
        int snapshots = motionSnapshotsSentSinceLastSample;
        motionSnapshotsSentSinceLastSample = 0;
        motionSnapshotsPerSecond = Mathf.RoundToInt(snapshots / Mathf.Max(0.001f, sampleInterval));

        CollectPhotonStats();
    }

    void ClearNetworkStats()
    {
        motionSnapshotsSentSinceLastSample = 0;
        motionSnapshotsPerSecond = 0;
        photonOutgoingPerSecond = 0;
        photonIncomingPerSecond = 0;
        photonTotalPerSecond = 0;
        ResetPhotonSample();
    }

    void CollectPhotonStats()
    {
        photonOutgoingPerSecond = 0;
        photonIncomingPerSecond = 0;
        photonTotalPerSecond = 0;

        LoadBalancingPeer peer = GetPhotonPeer();
        if (peer == null)
        {
            ResetPhotonSample();
            return;
        }

        if (!peer.TrafficStatsEnabled)
        {
            peer.TrafficStatsEnabled = true;
            trafficStatsEnabledByOverlay = true;
            ResetPhotonSample();
        }

        TrafficStatsGameLevel stats = peer.TrafficStatsGameLevel;
        float now = Time.unscaledTime;
        if (!hasPhotonSample)
        {
            lastPhotonSampleTime = now;
            lastPhotonOutgoingMessages = stats.TotalOutgoingMessageCount;
            lastPhotonIncomingMessages = stats.TotalIncomingMessageCount;
            lastPhotonTotalMessages = stats.TotalMessageCount;
            hasPhotonSample = true;
            return;
        }

        float interval = Mathf.Max(0.001f, now - lastPhotonSampleTime);
        photonOutgoingPerSecond = Mathf.RoundToInt((stats.TotalOutgoingMessageCount - lastPhotonOutgoingMessages) / interval);
        photonIncomingPerSecond = Mathf.RoundToInt((stats.TotalIncomingMessageCount - lastPhotonIncomingMessages) / interval);
        photonTotalPerSecond = Mathf.RoundToInt((stats.TotalMessageCount - lastPhotonTotalMessages) / interval);

        lastPhotonSampleTime = now;
        lastPhotonOutgoingMessages = stats.TotalOutgoingMessageCount;
        lastPhotonIncomingMessages = stats.TotalIncomingMessageCount;
        lastPhotonTotalMessages = stats.TotalMessageCount;
    }

    void SetPhotonTrafficStatsEnabled(bool enabled)
    {
        if (enabled)
            return;

        LoadBalancingPeer peer = GetPhotonPeer();
        if (trafficStatsEnabledByOverlay && peer != null)
            peer.TrafficStatsEnabled = false;

        trafficStatsEnabledByOverlay = false;
        ResetPhotonSample();
    }

    static LoadBalancingPeer GetPhotonPeer()
    {
        if (!PhotonNetwork.IsConnected || PhotonNetwork.NetworkingClient == null)
            return null;

        return PhotonNetwork.NetworkingClient.LoadBalancingPeer;
    }

    void ResetPhotonSample()
    {
        hasPhotonSample = false;
        lastPhotonSampleTime = 0f;
        lastPhotonOutgoingMessages = 0L;
        lastPhotonIncomingMessages = 0L;
        lastPhotonTotalMessages = 0L;
    }

    void RenderText(bool fpsEnabled, bool gcEnabled, bool sceneCountsEnabled, bool networkEnabled)
    {
        if (text == null)
            return;

        builder.Length = 0;
        builder.AppendLine("PERF DIAGNOSTICS");

        bool wroteSection = false;
        if (fpsEnabled)
        {
            builder.Append("FPS: ").Append(currentFps).Append("   Frame: ").Append(currentFrameMs.ToString("0.0")).AppendLine(" ms");
            wroteSection = true;
        }

        if (gcEnabled)
        {
            builder.Append("GC/frame: ").AppendLine(FormatBytes(gcAllocatedBytes));
            wroteSection = true;
        }

        if (sceneCountsEnabled)
        {
            if (wroteSection)
                builder.AppendLine();

            builder.Append("Moving objects: ").Append(movingObjects).AppendLine();
            builder.Append("Obstacle chunks: ").Append(obstacleChunks).AppendLine();
            builder.Append("Treasures: ").Append(treasures).AppendLine();
            builder.Append("Dropped cargo: ").Append(droppedCargo).AppendLine();
            builder.Append("Ship wrecks: ").Append(shipWrecks).AppendLine();
            builder.Append("Bullets: ").Append(bullets).AppendLine();
            builder.Append("Enemy bots: ").Append(enemyBots).AppendLine();
            builder.Append("Rigidbody2D: ").Append(rigidbodies2D).AppendLine();
            builder.Append("PhotonView: ").Append(photonViews).AppendLine();
            wroteSection = true;
        }

        if (networkEnabled)
        {
            if (wroteSection)
                builder.AppendLine();

            builder.Append("Role: ").Append(PhotonNetwork.IsMasterClient ? "HOST" : "CLIENT");
            builder.Append("   Players: ").Append(PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.PlayerCount : 0).AppendLine();
            builder.Append("Map: ").Append(RoomSettings.GetMapSizeMode()).Append("   Moving: ").Append(RoomSettings.GetMovingObjectsMode()).AppendLine();
            builder.Append("Motion snapshots/s: ").Append(motionSnapshotsPerSecond).AppendLine();
            builder.Append("Photon msg/s out: ").Append(photonOutgoingPerSecond).AppendLine();
            builder.Append("Photon msg/s in: ").Append(photonIncomingPerSecond).AppendLine();
            builder.Append("Photon msg/s total: ").Append(photonTotalPerSecond).AppendLine();

            LoadBalancingPeer peer = GetPhotonPeer();
            if (peer != null)
            {
                builder.Append("Ping: ").Append(peer.RoundTripTime).Append(" ms");
                builder.Append("   Resent: ").Append(peer.ResentReliableCommands);
            }

            wroteSection = true;
        }

        if (!wroteSection)
            builder.Append("No diagnostic modules enabled.");

        text.text = builder.ToString();
    }

    static int CountActive<T>() where T : Object
    {
        return FindObjectsByType<T>().Length;
    }

    static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024L)
            return (bytes / (1024f * 1024f)).ToString("0.00") + " MB";

        if (bytes >= 1024L)
            return (bytes / 1024f).ToString("0.0") + " KB";

        return bytes + " B";
    }
}
