using System;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SessionBrowserPanelUI : MonoBehaviour
{
    sealed class SessionRowView
    {
        public GameObject Root;
        public Image RowImage;
        public Button Button;
        public Outline Outline;
        public TMP_Text TitleText;
        public TMP_Text MetaText;
        public TMP_Text EffectsText;
        public TMP_Text StateText;
        public GameObject JoinPillObject;
        public Image JoinPillImage;
        public TMP_Text JoinText;
    }

    static SessionBrowserPanelUI instance;
    static bool visibleRequested;

    GameObject panelObject;
    Transform cachedCanvasTransform;
    TMP_Text statusText;
    TMP_Text emptyStateText;
    ScrollRect roomListScrollRect;
    RectTransform roomListContentRect;
    bool browserPanelVisibilityInitialized;
    bool browserPanelVisible;
    readonly List<GameObject> rowObjects = new List<GameObject>();
    readonly List<SessionRowView> rowPool = new List<SessionRowView>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

    static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("SessionBrowserPanelUI");
        instance = root.AddComponent<SessionBrowserPanelUI>();
        DontDestroyOnLoad(root);
    }

    public static void Prewarm()
    {
        if (!Application.isPlaying)
            return;

        EnsureInstance();
        if (instance == null)
            return;

        instance.EnsurePanel();
        instance.RefreshStatus();
        instance.RebuildRoomList(NetworkManager.GetSessionRooms());
        instance.RefreshVisibility();
    }

    public static bool IsVisible => visibleRequested && !PhotonNetwork.InRoom;

    public static void ShowBrowser()
    {
        visibleRequested = true;
        RoundMessageLayer.ClearAll();
        if (instance == null)
            return;

        instance.EnsurePanel();
        instance.RefreshStatus();
        instance.RebuildRoomList(NetworkManager.GetSessionRooms());
        instance.RefreshVisibility();
    }

    public static void HideBrowser()
    {
        visibleRequested = false;
        if (instance != null)
        {
            instance.RefreshVisibility();
        }
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
        SceneManager.sceneLoaded += OnSceneLoaded;
        NetworkManager.SessionRoomListChanged += OnSessionRoomListChanged;
        NetworkManager.SessionBrowserStatusChanged += OnSessionBrowserStatusChanged;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        NetworkManager.SessionRoomListChanged -= OnSessionRoomListChanged;
        NetworkManager.SessionBrowserStatusChanged -= OnSessionBrowserStatusChanged;

        if (instance == this)
            instance = null;
    }

    void Update()
    {
        if (panelObject == null || !panelObject.scene.IsValid())
            EnsurePanel();
        RefreshVisibility();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        cachedCanvasTransform = null;
        browserPanelVisibilityInitialized = false;
        EnsurePanel();
        RefreshVisibility();
        if (IsVisible)
        {
            RebuildRoomList(NetworkManager.GetSessionRooms());
            RefreshStatus();
        }
    }

    void OnSessionRoomListChanged(IReadOnlyList<NetworkManager.SessionRoomEntry> rooms)
    {
        EnsurePanel();
        RebuildRoomList(rooms);
    }

    void OnSessionBrowserStatusChanged(string status)
    {
        EnsurePanel();
        RefreshStatus(status);
    }

    void EnsurePanel()
    {
        Transform canvasTransform = GetCanvasTransform();
        if (canvasTransform == null)
            return;

        if (panelObject != null && panelObject.scene.IsValid())
        {
            if (panelObject.transform.parent != canvasTransform)
                panelObject.transform.SetParent(canvasTransform, false);

            return;
        }

        CreatePanel(canvasTransform);
        RefreshStatus();
        RebuildRoomList(NetworkManager.GetSessionRooms());
    }

    Transform GetCanvasTransform()
    {
        if (cachedCanvasTransform != null && cachedCanvasTransform.gameObject.scene.IsValid())
            return cachedCanvasTransform;

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
        cachedCanvasTransform = null;
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].name == "Canvas")
            {
                cachedCanvasTransform = canvases[i].transform;
                break;
            }
        }

        return cachedCanvasTransform;
    }

    void CreatePanel(Transform parent)
    {
        panelObject = new GameObject("SessionBrowserPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image background = panelObject.GetComponent<Image>();
        background.color = new Color(0.03f, 0.05f, 0.08f, 0.98f);

        CreateText(panelObject.transform, "BrowserTitle", "ACTIVE ROUNDS", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(820f, 54f), 44f, TextAlignmentOptions.Center);

        statusText = CreateText(panelObject.transform, "BrowserStatus", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(1080f, 44f), 28f, TextAlignmentOptions.Center);
        statusText.fontStyle = FontStyles.Normal;
        statusText.color = new Color(0.7f, 0.84f, 0.93f, 0.95f);

        Button backButton = CreateButton(panelObject.transform, "BrowserBackButton", "BACK", new Vector2(-410f, -176f), new Vector2(260f, 84f), OnBackClicked);
        StyleButton(backButton, new Color(0.5f, 0.08f, 0.1f, 0.98f), new Color(0.72f, 0.13f, 0.16f, 1f));

        Button refreshButton = CreateButton(panelObject.transform, "BrowserRefreshButton", "REFRESH", new Vector2(-112f, -176f), new Vector2(260f, 84f), OnRefreshClicked);
        StyleButton(refreshButton, new Color(0.16f, 0.36f, 0.44f, 0.95f), new Color(0.2f, 0.46f, 0.56f, 1f));

        Button newRoundButton = CreateButton(panelObject.transform, "BrowserNewRoundButton", "NEW ROUND", new Vector2(244f, -176f), new Vector2(380f, 88f), OnNewRoundClicked);
        StyleButton(newRoundButton, new Color(0.12f, 0.44f, 0.27f, 0.98f), new Color(0.16f, 0.56f, 0.34f, 1f));

        GameObject viewportObject = new GameObject("RoomListViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewportObject.transform.SetParent(panelObject.transform, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.5f, 0.5f);
        viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
        viewportRect.pivot = new Vector2(0.5f, 0.5f);
        viewportRect.anchoredPosition = new Vector2(0f, -132f);
        viewportRect.sizeDelta = new Vector2(1520f, 690f);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0.08f, 0.11f, 0.15f, 0.9f);

        GameObject contentObject = new GameObject("RoomListContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        roomListContentRect = contentObject.GetComponent<RectTransform>();
        roomListContentRect.anchorMin = new Vector2(0f, 1f);
        roomListContentRect.anchorMax = new Vector2(1f, 1f);
        roomListContentRect.pivot = new Vector2(0.5f, 1f);
        roomListContentRect.anchoredPosition = Vector2.zero;
        roomListContentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 28, 28);
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        roomListScrollRect = viewportObject.GetComponent<ScrollRect>();
        roomListScrollRect.horizontal = false;
        roomListScrollRect.vertical = true;
        roomListScrollRect.movementType = ScrollRect.MovementType.Clamped;
        roomListScrollRect.viewport = viewportRect;
        roomListScrollRect.content = roomListContentRect;
        roomListScrollRect.scrollSensitivity = 44f;

        emptyStateText = CreateText(panelObject.transform, "RoomListEmpty", "No sessions are visible right now. Create a new round to start one.", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -46f), new Vector2(1248f, 65f), 31.2f, TextAlignmentOptions.Center);
        emptyStateText.fontStyle = FontStyles.Normal;
        emptyStateText.color = new Color(0.72f, 0.79f, 0.87f, 0.92f);
    }

    void RefreshVisibility()
    {
        if (panelObject == null)
            return;

        bool shouldShow = visibleRequested && !PhotonNetwork.InRoom;
        bool changed = !browserPanelVisibilityInitialized || browserPanelVisible != shouldShow;
        browserPanelVisibilityInitialized = true;
        browserPanelVisible = shouldShow;

        if (panelObject.activeSelf != shouldShow)
            panelObject.SetActive(shouldShow);

        CanvasGroup canvasGroup = panelObject.GetComponent<CanvasGroup>();
        if (changed && canvasGroup != null)
        {
            canvasGroup.alpha = shouldShow ? 1f : 0f;
            canvasGroup.interactable = shouldShow;
            canvasGroup.blocksRaycasts = shouldShow;
        }

        if (shouldShow)
        {
            panelObject.transform.SetAsLastSibling();
        }
    }

    void RefreshStatus()
    {
        RefreshStatus(NetworkManager.GetSessionBrowserStatus());
    }

    void RefreshStatus(string status)
    {
        if (statusText == null)
            return;

        statusText.text = string.IsNullOrWhiteSpace(status)
            ? "Choose a round or create a new one."
            : status;
    }

    void RebuildRoomList(IReadOnlyList<NetworkManager.SessionRoomEntry> rooms)
    {
        if (roomListContentRect == null)
            return;

        for (int i = 0; i < rowObjects.Count; i++)
        {
            if (rowObjects[i] != null)
                rowObjects[i].SetActive(false);
        }

        rowObjects.Clear();

        bool hasRooms = rooms != null && rooms.Count > 0;
        if (emptyStateText != null)
            emptyStateText.gameObject.SetActive(!hasRooms);

        if (!hasRooms)
            return;

        for (int i = 0; i < rooms.Count; i++)
        {
            NetworkManager.SessionRoomEntry room = rooms[i];
            GameObject rowObject = CreateSessionRow(i, room);
            rowObjects.Add(rowObject);
        }

        Canvas.ForceUpdateCanvases();
        if (roomListScrollRect != null)
            roomListScrollRect.verticalNormalizedPosition = 1f;
    }

    GameObject CreateSessionRow(int rowIndex, NetworkManager.SessionRoomEntry room)
    {
        SessionRowView rowView = GetOrCreateSessionRowView(rowIndex);
        if (rowView == null || rowView.Root == null || room == null)
            return null;

        GameObject rowObject = rowView.Root;
        rowObject.name = "RoomRow_" + room.RoomName;
        rowObject.transform.SetParent(roomListContentRect, false);
        rowObject.transform.SetSiblingIndex(rowIndex);
        rowObject.SetActive(true);

        Color rowColor = room.CanJoin
            ? new Color(0.14f, 0.18f, 0.24f, 0.98f)
            : new Color(0.11f, 0.12f, 0.14f, 0.98f);

        rowView.RowImage.color = rowColor;
        rowView.Button.interactable = room.CanJoin;
        rowView.Button.onClick.RemoveAllListeners();
        string roomName = room.RoomName;
        rowView.Button.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayClick();
            OnRoomClicked(roomName);
        });

        ColorBlock rowColors = rowView.Button.colors;
        rowColors.normalColor = rowColor;
        rowColors.highlightedColor = room.CanJoin ? new Color(0.2f, 0.28f, 0.36f, 1f) : rowColor;
        rowColors.selectedColor = rowColors.highlightedColor;
        rowColors.pressedColor = room.CanJoin ? new Color(0.1f, 0.38f, 0.25f, 1f) : rowColor;
        rowColors.disabledColor = rowColor;
        rowView.Button.colors = rowColors;

        rowView.Outline.effectColor = room.CanJoin ? new Color(0.38f, 0.76f, 0.58f, 0.55f) : new Color(0.28f, 0.32f, 0.38f, 0.42f);
        rowView.TitleText.text = room.DisplayName;
        rowView.MetaText.text = BuildMetaLine(room);

        bool hasEffects = !string.IsNullOrWhiteSpace(room.ActiveEffectsLabel);
        rowView.EffectsText.gameObject.SetActive(hasEffects);
        if (hasEffects)
        {
            rowView.EffectsText.text = room.State == RoomSettings.SessionStateInPlay
                ? "Effects: " + room.ActiveEffectsLabel
                : room.ActiveEffectsLabel;
        }

        rowView.StateText.text = BuildStateLabel(room);
        rowView.StateText.color = room.BlockedByLocalDeath
            ? new Color(0.95f, 0.38f, 0.34f, 1f)
            : room.State == RoomSettings.SessionStateInPlay
            ? new Color(0.94f, 0.75f, 0.33f, 1f)
            : new Color(0.38f, 0.83f, 0.62f, 1f);

        rowView.JoinPillImage.color = room.CanJoin
            ? new Color(0.1f, 0.48f, 0.3f, 0.98f)
            : new Color(0.2f, 0.22f, 0.25f, 0.9f);
        rowView.JoinText.text = BuildJoinLabel(room);
        rowView.JoinText.color = room.CanJoin
            ? new Color(0.95f, 0.98f, 1f, 1f)
            : new Color(0.62f, 0.64f, 0.68f, 1f);

        return rowObject;
    }

    SessionRowView GetOrCreateSessionRowView(int rowIndex)
    {
        while (rowPool.Count <= rowIndex)
            rowPool.Add(null);

        SessionRowView rowView = rowPool[rowIndex];
        if (rowView != null && rowView.Root != null)
            return rowView;

        GameObject rowObject = new GameObject("RoomRowPool_" + rowIndex, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        rowObject.transform.SetParent(roomListContentRect, false);

        RectTransform rect = rowObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 188f);
        LayoutElement layout = rowObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 188f;

        rowView = new SessionRowView
        {
            Root = rowObject,
            RowImage = rowObject.GetComponent<Image>(),
            Button = rowObject.GetComponent<Button>(),
            Outline = rowObject.AddComponent<Outline>()
        };
        rowView.Outline.effectDistance = new Vector2(3f, -3f);
        rowView.Outline.useGraphicAlpha = true;

        rowView.TitleText = CreateText(rowObject.transform, "RoomTitle", string.Empty, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -34f), new Vector2(860f, 46f), 33f, TextAlignmentOptions.Left);
        rowView.TitleText.rectTransform.pivot = new Vector2(0f, 0.5f);
        rowView.TitleText.overflowMode = TextOverflowModes.Truncate;

        rowView.MetaText = CreateText(rowObject.transform, "RoomMeta", string.Empty, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -84f), new Vector2(930f, 34f), 22f, TextAlignmentOptions.Left);
        rowView.MetaText.rectTransform.pivot = new Vector2(0f, 0.5f);
        rowView.MetaText.fontStyle = FontStyles.Normal;
        rowView.MetaText.color = new Color(0.72f, 0.81f, 0.9f, 0.95f);
        rowView.MetaText.overflowMode = TextOverflowModes.Truncate;

        rowView.EffectsText = CreateText(rowObject.transform, "RoomEffects", string.Empty, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -128f), new Vector2(930f, 32f), 21f, TextAlignmentOptions.Left);
        rowView.EffectsText.rectTransform.pivot = new Vector2(0f, 0.5f);
        rowView.EffectsText.fontStyle = FontStyles.Bold;
        rowView.EffectsText.color = new Color(0.72f, 0.58f, 1f, 0.98f);
        rowView.EffectsText.overflowMode = TextOverflowModes.Truncate;

        rowView.StateText = CreateText(rowObject.transform, "RoomState", string.Empty, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-304f, -44f), new Vector2(280f, 46f), 25f, TextAlignmentOptions.Center);
        rowView.StateText.rectTransform.pivot = new Vector2(1f, 0.5f);
        rowView.StateText.overflowMode = TextOverflowModes.Truncate;

        rowView.JoinPillObject = new GameObject("RoomJoinPill", typeof(RectTransform), typeof(Image));
        rowView.JoinPillObject.transform.SetParent(rowObject.transform, false);
        RectTransform joinPillRect = rowView.JoinPillObject.GetComponent<RectTransform>();
        joinPillRect.anchorMin = new Vector2(1f, 1f);
        joinPillRect.anchorMax = new Vector2(1f, 1f);
        joinPillRect.pivot = new Vector2(1f, 0.5f);
        joinPillRect.anchoredPosition = new Vector2(-34f, -108f);
        joinPillRect.sizeDelta = new Vector2(194f, 68f);

        rowView.JoinPillImage = rowView.JoinPillObject.GetComponent<Image>();
        rowView.JoinPillImage.raycastTarget = false;
        rowView.JoinText = CreateText(rowView.JoinPillObject.transform, "RoomJoin", string.Empty, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 26f, TextAlignmentOptions.Center);
        rowView.JoinText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rowView.JoinText.fontStyle = FontStyles.Bold;

        rowPool[rowIndex] = rowView;
        return rowView;
    }

    string BuildMetaLine(NetworkManager.SessionRoomEntry room)
    {
        string meta = "Host: " + room.HostName + "    Map: " + room.MapName + "    Players: " + room.PlayerCount + "/" + room.MaxPlayers;
        if (room.RemainingTimeSeconds.HasValue)
        {
            meta += "    Remaining: " + FormatDuration(room.RemainingTimeSeconds.Value);
        }

        if (room.BlockedByLocalDeath)
            meta += "    You cannot rejoin this round.";
        else if (!room.CanJoin && !string.IsNullOrWhiteSpace(room.BlockReason))
            meta += "    " + room.BlockReason;

        return meta;
    }

    string BuildStateLabel(NetworkManager.SessionRoomEntry room)
    {
        if (room.BlockedByLocalDeath || (!room.CanJoin && !string.IsNullOrWhiteSpace(room.BlockReason)))
            return string.IsNullOrWhiteSpace(room.BlockReason) ? "ENDED" : room.BlockReason;

        if (room.State == RoomSettings.SessionStateInPlay)
            return "IN PLAY";

        return "IN LOBBY";
    }

    string BuildJoinLabel(NetworkManager.SessionRoomEntry room)
    {
        if (room.BlockedByLocalDeath)
            return "ENDED";

        if (!room.CanJoin && !string.IsNullOrWhiteSpace(room.BlockReason))
            return "LOCKED";

        return room.CanJoin ? "JOIN" : "FULL";
    }

    string FormatDuration(float seconds)
    {
        int clamped = Mathf.Max(0, Mathf.CeilToInt(seconds));
        int minutes = clamped / 60;
        int remainingSeconds = clamped % 60;
        return minutes.ToString("00") + ":" + remainingSeconds.ToString("00");
    }

    void OnBackClicked()
    {
        NetworkManager.CancelSessionStart();
        HideBrowser();
    }

    void OnRefreshClicked()
    {
        NetworkManager.RefreshSessionBrowser();
    }

    void OnNewRoundClicked()
    {
        NetworkManager.CreateNewRound();
    }

    void OnRoomClicked(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
            return;

        NetworkManager.JoinSession(roomName);
    }

    Button CreateButton(Transform parent, string objectName, string label, Vector2 anchoredPosition, Vector2 size, Action onClick)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(() =>
        {
            if (!ButtonClickSoundHook.ShouldSuppressClickSound(objectName, label))
                AudioManager.Instance?.PlayClick();
            onClick?.Invoke();
        });

        TMP_Text text = CreateText(buttonObject.transform, objectName + "Text", label, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 27f, TextAlignmentOptions.Center);
        text.fontStyle = FontStyles.Bold;
        text.enableAutoSizing = true;
        text.fontSizeMin = 22f;
        text.fontSizeMax = 30f;
        text.margin = new Vector4(12f, 6f, 12f, 6f);
        return button;
    }

    void StyleButton(Button button, Color normalColor, Color highlightedColor)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>();
        if (image != null)
            image.color = normalColor;

        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.selectedColor = highlightedColor;
        colors.highlightedColor = highlightedColor;
        colors.pressedColor = Color.Lerp(highlightedColor, Color.black, 0.15f);
        colors.disabledColor = new Color(0.26f, 0.28f, 0.31f, 0.8f);
        button.colors = colors;

        Outline outline = button.GetComponent<Outline>();
        if (outline == null)
            outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.Lerp(highlightedColor, Color.white, 0.25f) * new Color(1f, 1f, 1f, 0.72f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;
    }

    TMP_Text CreateText(Transform parent, string objectName, string value, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(0.94f, 0.97f, 1f, 1f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.fontStyle = FontStyles.Bold;

        TMP_Text reference = FindAnyObjectByType<TextMeshProUGUI>();
        if (reference != null)
        {
            text.font = reference.font;
            text.fontSharedMaterial = reference.fontSharedMaterial;
        }

        return text;
    }
}
