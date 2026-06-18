using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum RoundMessagePriority
{
    Info = 0,
    Event = 10,
    Warning = 20,
    Danger = 30
}

public sealed class RoundMessageLayer : MonoBehaviour
{
    const float ReferenceWidth = 1920f;
    const float ReferenceHeight = 1080f;
    const int MaxTopCenterQueuedMessages = 5;
    const int MaxLeftFeedMessages = 3;
    const float TopCenterMinVisibleSeconds = 2.4f;
    const float TopCenterQueueGapSeconds = 0.08f;

    static RoundMessageLayer instance;

    Canvas canvas;
    CanvasGroup topWarningGroup;
    CanvasGroup topCenterGroup;
    CanvasGroup persistentObjectiveGroup;
    CanvasGroup statusFeedGroup;
    Image topWarningBackground;
    Image topCenterBackground;
    Image persistentObjectiveBackground;
    Image statusFeedBackground;
    Image statusFeedAccent;
    RectTransform topCenterRect;
    TextMeshProUGUI topWarningText;
    TextMeshProUGUI topCenterText;
    TextMeshProUGUI persistentObjectiveText;
    TextMeshProUGUI statusFeedTitleText;
    TextMeshProUGUI statusFeedLabelText;
    RectTransform leftFeedRootRect;
    LeftFeedView[] leftFeedViews;
    Coroutine topCenterRoutine;
    string activeTopCenterMessage;
    string persistentObjectiveOwnerKey;
    string persistentObjectiveMessage;
    string activeStatusTitle;
    string activeStatusLabel;
    string activeWarningMessage;
    RoundMessagePriority activeWarningPriority;
    float activeWarningUntil;
    RoundMessagePriority activeStatusPriority;
    Color activeStatusAccentColor;
    float activeStatusStartedAt;
    float activeStatusUntil;
    readonly Queue<TopCenterMessage> topCenterQueue = new Queue<TopCenterMessage>(MaxTopCenterQueuedMessages);
    readonly List<LeftFeedMessage> leftFeedMessages = new List<LeftFeedMessage>(MaxLeftFeedMessages);

    sealed class TopCenterMessage
    {
        public string Message;
        public float Seconds;
        public Color AccentColor;
    }

    sealed class LeftFeedMessage
    {
        public string Title;
        public string Label;
        public Sprite Icon;
        public float CreatedAt;
        public float ExpiresAt;
    }

    sealed class LeftFeedView
    {
        public GameObject RootObject;
        public CanvasGroup Group;
        public Image Background;
        public Image Icon;
        public TextMeshProUGUI TitleText;
        public TextMeshProUGUI LabelText;
        public RectTransform Rect;
    }

    public static void ShowTopCenter(string message, float seconds = 2.6f)
    {
        ShowTopCenter(message, seconds, new Color(1f, 0.72f, 0.18f, 1f));
    }

    public static void ShowTopCenter(string message, float seconds, Color accentColor)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        EnsureInstance();
        if (instance != null)
            instance.ShowTopCenterInternal(message, seconds, accentColor);
    }

    public static void ShowWarning(string message, RoundMessagePriority priority = RoundMessagePriority.Warning, float holdSeconds = 0.28f)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        EnsureInstance();
        if (instance != null)
            instance.ShowWarningInternal(message, priority, holdSeconds);
    }

    public static void ClearWarning(RoundMessagePriority priority)
    {
        if (instance != null)
            instance.ClearWarningInternal(priority);
    }

    public static void SetPersistentObjective(string ownerKey, string message)
    {
        if (string.IsNullOrWhiteSpace(ownerKey))
            return;

        if (string.IsNullOrWhiteSpace(message))
        {
            ClearPersistentObjective(ownerKey);
            return;
        }

        EnsureInstance();
        if (instance != null)
            instance.SetPersistentObjectiveInternal(ownerKey, message);
    }

    public static void ClearPersistentObjective(string ownerKey)
    {
        if (string.IsNullOrWhiteSpace(ownerKey) || instance == null)
            return;

        instance.ClearPersistentObjectiveInternal(ownerKey);
    }

    public static void ClearAll()
    {
        if (instance == null)
            return;

        instance.ClearAllInternal();
    }

    public static bool SetPersistentObjectiveIfEmpty(string ownerKey, string message)
    {
        if (string.IsNullOrWhiteSpace(ownerKey) || string.IsNullOrWhiteSpace(message))
            return false;

        EnsureInstance();
        return instance != null && instance.SetPersistentObjectiveIfEmptyInternal(ownerKey, message);
    }

    public static void ShowStatusFeed(string title, string label, RoundMessagePriority priority = RoundMessagePriority.Warning, float seconds = 3.6f)
    {
        ShowStatusFeed(title, label, priority, seconds, GetDefaultStatusAccent(priority));
    }

    public static void ShowStatusFeed(string title, string label, RoundMessagePriority priority, float seconds, Color accentColor)
    {
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(label))
            return;

        EnsureInstance();
        if (instance != null)
            instance.ShowStatusFeedInternal(title, label, priority, seconds, accentColor);
    }

    public static void ShowLeftFeed(string title, string label, Sprite icon, float seconds = 2f)
    {
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(label) && icon == null)
            return;

        EnsureInstance();
        if (instance != null)
            instance.ShowLeftFeedInternal(title, label, icon, seconds);
    }

    static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject existing = GameObject.Find("RoundMessageLayer");
        if (existing != null && existing.TryGetComponent(out RoundMessageLayer existingLayer))
        {
            instance = existingLayer;
            instance.EnsureUi();
            return;
        }

        GameObject root = new GameObject("RoundMessageLayer");
        instance = root.AddComponent<RoundMessageLayer>();
        DontDestroyOnLoad(root);
        instance.EnsureUi();
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
        EnsureUi();
    }

    void Update()
    {
        UpdateWarningVisual();
        UpdateStatusFeedVisual();
        UpdateLeftFeedVisual();
    }

    void EnsureUi()
    {
        if (canvas != null && topWarningText != null && topCenterText != null && topCenterRect != null && persistentObjectiveText != null && statusFeedGroup != null && leftFeedViews != null)
            return;

        canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 21000;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;

        CreateTopWarning();
        CreateTopCenter();
        CreatePersistentObjective();
        CreateStatusFeed();
        CreateLeftFeed();
    }

    void CreateTopWarning()
    {
        GameObject panel = CreatePanel("TopWarning", new Vector2(0f, -68f), new Vector2(1080f, 66f), new Color(0.16f, 0.035f, 0.02f, 0.84f));
        topWarningGroup = panel.GetComponent<CanvasGroup>();
        topWarningBackground = panel.GetComponent<Image>();
        topWarningText = CreateText(panel.transform, "Text", 36f, TextAlignmentOptions.Center, FontStyles.Bold);
        topWarningText.characterSpacing = 1.8f;
        topWarningText.enableAutoSizing = true;
        topWarningText.fontSizeMin = 28f;
        topWarningText.fontSizeMax = 36f;
        topWarningText.maxVisibleLines = 1;
        topWarningText.overflowMode = TextOverflowModes.Ellipsis;
        topWarningGroup.alpha = 0f;
        panel.SetActive(false);
    }

    void CreateTopCenter()
    {
        GameObject panel = CreatePanel("TopCenter", new Vector2(0f, -142f), new Vector2(1120f, 84f), new Color(0.12f, 0.085f, 0.02f, 0.82f));
        topCenterRect = panel.GetComponent<RectTransform>();
        topCenterGroup = panel.GetComponent<CanvasGroup>();
        topCenterBackground = panel.GetComponent<Image>();
        topCenterText = CreateText(panel.transform, "Text", 40f, TextAlignmentOptions.Center, FontStyles.Bold);
        topCenterText.enableAutoSizing = true;
        topCenterText.fontSizeMin = 30f;
        topCenterText.fontSizeMax = 40f;
        topCenterText.maxVisibleLines = 2;
        topCenterText.overflowMode = TextOverflowModes.Truncate;
        topCenterGroup.alpha = 0f;
        panel.SetActive(false);
    }

    void CreatePersistentObjective()
    {
        GameObject panel = CreatePanel("PersistentObjective", new Vector2(0f, -146f), new Vector2(1180f, 98f), new Color(0.02f, 0.1f, 0.16f, 0.76f));
        persistentObjectiveGroup = panel.GetComponent<CanvasGroup>();
        persistentObjectiveBackground = panel.GetComponent<Image>();
        persistentObjectiveText = CreateText(panel.transform, "Text", 30f, TextAlignmentOptions.Center, FontStyles.Bold);
        persistentObjectiveText.enableAutoSizing = true;
        persistentObjectiveText.fontSizeMin = 22f;
        persistentObjectiveText.fontSizeMax = 30f;
        persistentObjectiveText.maxVisibleLines = 3;
        persistentObjectiveText.overflowMode = TextOverflowModes.Truncate;
        persistentObjectiveText.color = new Color(0.72f, 0.94f, 1f, 1f);
        persistentObjectiveGroup.alpha = 0f;
        panel.SetActive(false);
    }

    void CreateStatusFeed()
    {
        GameObject panel = CreatePanel(
            "StatusFeed",
            new Vector2(-28f, -128f),
            new Vector2(420f, 104f),
            new Color(0.12f, 0.035f, 0.03f, 0.9f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f));

        statusFeedGroup = panel.GetComponent<CanvasGroup>();
        statusFeedBackground = panel.GetComponent<Image>();

        Transform existingAccent = panel.transform.Find("Accent");
        GameObject accentObject = existingAccent != null
            ? existingAccent.gameObject
            : new GameObject("Accent", typeof(RectTransform), typeof(Image));
        accentObject.transform.SetParent(panel.transform, false);
        RectTransform accentRect = accentObject.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(8f, 0f);
        statusFeedAccent = accentObject.GetComponent<Image>();
        statusFeedAccent.raycastTarget = false;

        statusFeedTitleText = CreateText(panel.transform, "Title", 20f, TextAlignmentOptions.Left, FontStyles.Bold);
        RectTransform titleRect = statusFeedTitleText.rectTransform;
        titleRect.offsetMin = new Vector2(26f, 62f);
        titleRect.offsetMax = new Vector2(-20f, -10f);
        statusFeedTitleText.characterSpacing = 1.3f;
        statusFeedTitleText.textWrappingMode = TextWrappingModes.NoWrap;
        statusFeedTitleText.overflowMode = TextOverflowModes.Ellipsis;

        statusFeedLabelText = CreateText(panel.transform, "Label", 30f, TextAlignmentOptions.Left, FontStyles.Bold);
        RectTransform labelRect = statusFeedLabelText.rectTransform;
        labelRect.offsetMin = new Vector2(26f, 12f);
        labelRect.offsetMax = new Vector2(-20f, -42f);
        statusFeedLabelText.enableAutoSizing = true;
        statusFeedLabelText.fontSizeMin = 24f;
        statusFeedLabelText.fontSizeMax = 30f;
        statusFeedLabelText.maxVisibleLines = 1;
        statusFeedLabelText.overflowMode = TextOverflowModes.Ellipsis;
        statusFeedLabelText.textWrappingMode = TextWrappingModes.NoWrap;

        statusFeedGroup.alpha = 0f;
        panel.SetActive(false);
    }

    void CreateLeftFeed()
    {
        Transform existing = transform.Find("LeftFeed");
        GameObject root = existing != null
            ? existing.gameObject
            : new GameObject("LeftFeed", typeof(RectTransform));
        root.transform.SetParent(transform, false);

        leftFeedRootRect = root.GetComponent<RectTransform>();
        leftFeedRootRect.anchorMin = new Vector2(0f, 1f);
        leftFeedRootRect.anchorMax = new Vector2(0f, 1f);
        leftFeedRootRect.pivot = new Vector2(0f, 1f);
        leftFeedRootRect.anchoredPosition = new Vector2(24f, -270f);
        leftFeedRootRect.sizeDelta = new Vector2(320f, 260f);

        leftFeedViews = new LeftFeedView[MaxLeftFeedMessages];
        for (int i = 0; i < leftFeedViews.Length; i++)
            leftFeedViews[i] = CreateLeftFeedView(root.transform, i);
    }

    LeftFeedView CreateLeftFeedView(Transform parent, int index)
    {
        Transform existing = parent.Find("Entry" + index);
        GameObject panel = existing != null
            ? existing.gameObject
            : new GameObject("Entry" + index, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panel.transform.SetParent(parent, false);

        LeftFeedView view = new LeftFeedView
        {
            RootObject = panel,
            Rect = panel.GetComponent<RectTransform>(),
            Background = panel.GetComponent<Image>(),
            Group = panel.GetComponent<CanvasGroup>()
        };

        view.Rect.anchorMin = new Vector2(0f, 1f);
        view.Rect.anchorMax = new Vector2(0f, 1f);
        view.Rect.pivot = new Vector2(0f, 1f);
        view.Rect.anchoredPosition = new Vector2(0f, -index * 82f);
        view.Rect.sizeDelta = new Vector2(300f, 104f);

        view.Background.color = new Color(0.045f, 0.08f, 0.13f, 0.9f);
        view.Background.raycastTarget = false;

        view.Group.blocksRaycasts = false;
        view.Group.interactable = false;
        view.Group.alpha = 0f;

        GameObject titleObject = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Shadow));
        titleObject.transform.SetParent(panel.transform, false);
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(16f, -32f);
        titleRect.offsetMax = new Vector2(-16f, -7f);
        view.TitleText = titleObject.GetComponent<TextMeshProUGUI>();
        ConfigureText(view.TitleText, 18f, TextAlignmentOptions.Left, FontStyles.Bold);
        view.TitleText.color = new Color(0.84f, 0.94f, 1f, 0.96f);
        view.TitleText.textWrappingMode = TextWrappingModes.NoWrap;

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(panel.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(48f, -16f);
        iconRect.sizeDelta = new Vector2(58f, 58f);
        view.Icon = iconObject.GetComponent<Image>();
        view.Icon.preserveAspect = true;
        view.Icon.raycastTarget = false;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Shadow));
        labelObject.transform.SetParent(panel.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(88f, 12f);
        labelRect.offsetMax = new Vector2(-14f, -35f);
        view.LabelText = labelObject.GetComponent<TextMeshProUGUI>();
        ConfigureText(view.LabelText, 20f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        view.LabelText.enableAutoSizing = true;
        view.LabelText.fontSizeMin = 17f;
        view.LabelText.fontSizeMax = 20f;
        view.LabelText.maxVisibleLines = 2;
        view.LabelText.overflowMode = TextOverflowModes.Ellipsis;
        view.LabelText.textWrappingMode = TextWrappingModes.Normal;

        panel.SetActive(false);
        return view;
    }

    GameObject CreatePanel(string name, Vector2 position, Vector2 size, Color backgroundColor)
    {
        return CreatePanel(name, position, size, backgroundColor, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
    }

    GameObject CreatePanel(string name, Vector2 position, Vector2 size, Color backgroundColor, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        Transform existing = transform.Find(name);
        GameObject panel = existing != null
            ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panel.transform.SetParent(transform, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = panel.GetComponent<Image>();
        image.color = backgroundColor;
        image.raycastTarget = false;

        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        return panel;
    }

    TextMeshProUGUI CreateText(Transform parent, string name, float fontSize, TextAlignmentOptions alignment, FontStyles fontStyle)
    {
        Transform existing = parent.Find(name);
        GameObject textObject = existing != null
            ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Shadow));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(24f, 8f);
        rect.offsetMax = new Vector2(-24f, -8f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        ConfigureText(text, fontSize, alignment, fontStyle);

        Shadow shadow = textObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.78f);
        shadow.effectDistance = new Vector2(2.4f, -2.4f);
        return text;
    }

    void ConfigureText(TextMeshProUGUI text, float fontSize, TextAlignmentOptions alignment, FontStyles fontStyle)
    {
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.color = new Color(1f, 0.94f, 0.72f, 1f);
        text.raycastTarget = false;

        TMP_Text reference = FindAnyObjectByType<TMP_Text>();
        if (reference != null && reference != text)
        {
            text.font = reference.font;
            text.fontSharedMaterial = reference.fontSharedMaterial;
        }
    }

    void ShowTopCenterInternal(string message, float seconds, Color accentColor)
    {
        EnsureUi();

        if (string.Equals(activeTopCenterMessage, message, System.StringComparison.Ordinal) ||
            IsTopCenterMessageQueued(message))
        {
            return;
        }

        if (topCenterQueue.Count >= MaxTopCenterQueuedMessages)
            topCenterQueue.Dequeue();

        topCenterQueue.Enqueue(new TopCenterMessage
        {
            Message = message,
            Seconds = Mathf.Max(TopCenterMinVisibleSeconds, seconds),
            AccentColor = accentColor
        });

        if (topCenterRoutine == null)
            topCenterRoutine = StartCoroutine(ProcessTopCenterQueue());
    }

    IEnumerator ProcessTopCenterQueue()
    {
        while (topCenterQueue.Count > 0)
        {
            TopCenterMessage queued = topCenterQueue.Dequeue();
            yield return TopCenterRoutine(queued.Message, queued.Seconds, queued.AccentColor);
            if (topCenterQueue.Count > 0)
                yield return new WaitForSecondsRealtime(TopCenterQueueGapSeconds);
        }

        topCenterRoutine = null;
    }

    bool IsTopCenterMessageQueued(string message)
    {
        foreach (TopCenterMessage queued in topCenterQueue)
        {
            if (queued != null && string.Equals(queued.Message, message, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    IEnumerator TopCenterRoutine(string message, float duration, Color accentColor)
    {
        activeTopCenterMessage = message;
        ApplyTopCenterLayout(message);
        topCenterText.text = message;
        topCenterText.color = new Color(1f, 0.94f, 0.72f, 1f);
        if (topCenterBackground != null)
            topCenterBackground.color = new Color(accentColor.r * 0.24f, accentColor.g * 0.2f, accentColor.b * 0.12f, 0.84f);

        topCenterGroup.gameObject.SetActive(true);
        topCenterGroup.gameObject.transform.SetAsLastSibling();

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float fadeIn = Mathf.Clamp01(elapsed / 0.18f);
            float fadeOut = Mathf.Clamp01((duration - elapsed) / 0.42f);
            topCenterGroup.alpha = Mathf.Min(fadeIn, fadeOut);
            yield return null;
        }

        topCenterGroup.alpha = 0f;
        topCenterGroup.gameObject.SetActive(false);
        topCenterText.text = string.Empty;
        activeTopCenterMessage = null;
    }

    void ApplyTopCenterLayout(string message)
    {
        bool longMessage = !string.IsNullOrWhiteSpace(message) && message.Length > 56;

        if (topCenterRect != null)
        {
            topCenterRect.anchoredPosition = longMessage ? new Vector2(0f, -140f) : new Vector2(0f, -142f);
            topCenterRect.sizeDelta = longMessage ? new Vector2(1180f, 122f) : new Vector2(1120f, 84f);
        }

        if (topCenterText != null)
        {
            topCenterText.fontSizeMin = longMessage ? 24f : 30f;
            topCenterText.fontSizeMax = longMessage ? 36f : 40f;
            topCenterText.maxVisibleLines = longMessage ? 3 : 2;
            topCenterText.overflowMode = TextOverflowModes.Truncate;
        }
    }

    void ShowWarningInternal(string message, RoundMessagePriority priority, float holdSeconds)
    {
        EnsureUi();
        bool warningExpired = Time.unscaledTime > activeWarningUntil;
        if (!warningExpired && priority < activeWarningPriority)
            return;

        activeWarningMessage = message;
        activeWarningPriority = priority;
        activeWarningUntil = Time.unscaledTime + Mathf.Max(0.1f, holdSeconds);
    }

    void ClearWarningInternal(RoundMessagePriority priority)
    {
        if (activeWarningPriority != priority)
            return;

        activeWarningMessage = null;
        activeWarningUntil = -1f;
        if (topWarningGroup != null)
        {
            topWarningGroup.alpha = 0f;
            topWarningGroup.gameObject.SetActive(false);
        }
    }

    void UpdateWarningVisual()
    {
        if (topWarningGroup == null || topWarningText == null)
            return;

        bool visible = !string.IsNullOrWhiteSpace(activeWarningMessage) && Time.unscaledTime <= activeWarningUntil;
        if (!visible)
        {
            topWarningGroup.alpha = 0f;
            topWarningGroup.gameObject.SetActive(false);
            return;
        }

        if (!topWarningGroup.gameObject.activeSelf)
            topWarningGroup.gameObject.SetActive(true);

        topWarningGroup.gameObject.transform.SetAsLastSibling();
        topWarningText.text = activeWarningMessage;

        float pulse = Mathf.PingPong(Time.unscaledTime * (activeWarningPriority == RoundMessagePriority.Danger ? 5.8f : 3.4f), 1f);
        topWarningGroup.alpha = Mathf.Lerp(0.78f, 1f, pulse);

        if (topWarningBackground != null)
        {
            topWarningBackground.color = activeWarningPriority == RoundMessagePriority.Danger
                ? Color.Lerp(new Color(0.18f, 0.02f, 0.015f, 0.82f), new Color(0.38f, 0.04f, 0.02f, 0.92f), pulse)
                : Color.Lerp(new Color(0.16f, 0.07f, 0.02f, 0.78f), new Color(0.28f, 0.12f, 0.02f, 0.88f), pulse);
        }

        topWarningText.color = activeWarningPriority == RoundMessagePriority.Danger
            ? Color.Lerp(new Color(1f, 0.42f, 0.24f, 1f), new Color(1f, 0.9f, 0.42f, 1f), pulse)
            : Color.Lerp(new Color(1f, 0.7f, 0.28f, 1f), new Color(1f, 0.95f, 0.52f, 1f), pulse);
    }

    void ClearAllInternal()
    {
        activeTopCenterMessage = null;
        topCenterQueue.Clear();
        if (topCenterRoutine != null)
        {
            StopCoroutine(topCenterRoutine);
            topCenterRoutine = null;
        }

        activeWarningMessage = null;
        activeWarningUntil = -1f;
        activeStatusTitle = null;
        activeStatusLabel = null;
        activeStatusUntil = -1f;
        persistentObjectiveOwnerKey = null;
        persistentObjectiveMessage = null;
        leftFeedMessages.Clear();

        if (topCenterGroup != null)
        {
            topCenterGroup.alpha = 0f;
            topCenterGroup.gameObject.SetActive(false);
        }

        if (topCenterText != null)
            topCenterText.text = string.Empty;

        if (topWarningGroup != null)
        {
            topWarningGroup.alpha = 0f;
            topWarningGroup.gameObject.SetActive(false);
        }

        if (topWarningText != null)
            topWarningText.text = string.Empty;

        if (persistentObjectiveText != null)
            persistentObjectiveText.text = string.Empty;
        ApplyPersistentObjective();

        if (statusFeedGroup != null)
        {
            statusFeedGroup.alpha = 0f;
            statusFeedGroup.gameObject.SetActive(false);
        }

        if (statusFeedTitleText != null)
            statusFeedTitleText.text = string.Empty;
        if (statusFeedLabelText != null)
            statusFeedLabelText.text = string.Empty;

        if (leftFeedViews != null)
        {
            for (int i = 0; i < leftFeedViews.Length; i++)
            {
                LeftFeedView view = leftFeedViews[i];
                if (view == null || view.RootObject == null)
                    continue;

                if (view.Group != null)
                    view.Group.alpha = 0f;
                view.RootObject.SetActive(false);
            }
        }
    }

    void SetPersistentObjectiveInternal(string ownerKey, string message)
    {
        EnsureUi();
        if (string.Equals(persistentObjectiveOwnerKey, ownerKey, System.StringComparison.Ordinal) &&
            string.Equals(persistentObjectiveMessage, message, System.StringComparison.Ordinal))
        {
            return;
        }

        persistentObjectiveOwnerKey = ownerKey;
        persistentObjectiveMessage = message;
        ApplyPersistentObjective();
    }

    bool SetPersistentObjectiveIfEmptyInternal(string ownerKey, string message)
    {
        EnsureUi();
        if (!string.IsNullOrWhiteSpace(persistentObjectiveOwnerKey) &&
            !string.Equals(persistentObjectiveOwnerKey, ownerKey, System.StringComparison.Ordinal))
        {
            return false;
        }

        SetPersistentObjectiveInternal(ownerKey, message);
        return true;
    }

    void ClearPersistentObjectiveInternal(string ownerKey)
    {
        if (!string.Equals(persistentObjectiveOwnerKey, ownerKey, System.StringComparison.Ordinal))
            return;

        persistentObjectiveOwnerKey = null;
        persistentObjectiveMessage = null;
        ApplyPersistentObjective();
    }

    void ApplyPersistentObjective()
    {
        if (persistentObjectiveGroup == null || persistentObjectiveText == null)
            return;

        bool visible = !string.IsNullOrWhiteSpace(persistentObjectiveMessage);
        persistentObjectiveText.text = visible ? persistentObjectiveMessage : string.Empty;
        persistentObjectiveGroup.alpha = visible ? 0.94f : 0f;
        persistentObjectiveGroup.gameObject.SetActive(visible);
        if (visible)
        {
            if (persistentObjectiveBackground != null)
                persistentObjectiveBackground.color = new Color(0.02f, 0.1f, 0.16f, 0.76f);
        }
    }

    void ShowStatusFeedInternal(string title, string label, RoundMessagePriority priority, float seconds, Color accentColor)
    {
        EnsureUi();

        bool currentVisible = !string.IsNullOrWhiteSpace(activeStatusTitle) && Time.unscaledTime <= activeStatusUntil;
        if (currentVisible && priority < activeStatusPriority)
            return;

        activeStatusTitle = string.IsNullOrWhiteSpace(title) ? "STATUS" : title;
        activeStatusLabel = label ?? string.Empty;
        activeStatusPriority = priority;
        activeStatusAccentColor = accentColor;
        activeStatusStartedAt = Time.unscaledTime;
        activeStatusUntil = activeStatusStartedAt + Mathf.Max(1.2f, seconds);
        UpdateStatusFeedVisual();
    }

    void UpdateStatusFeedVisual()
    {
        if (statusFeedGroup == null || statusFeedTitleText == null || statusFeedLabelText == null)
            return;

        bool visible = !string.IsNullOrWhiteSpace(activeStatusTitle) && Time.unscaledTime <= activeStatusUntil;
        if (!visible)
        {
            statusFeedGroup.alpha = 0f;
            statusFeedGroup.gameObject.SetActive(false);
            return;
        }

        if (!statusFeedGroup.gameObject.activeSelf)
            statusFeedGroup.gameObject.SetActive(true);

        statusFeedGroup.gameObject.transform.SetAsLastSibling();
        statusFeedTitleText.text = activeStatusTitle;
        statusFeedLabelText.text = activeStatusLabel;

        float age = Mathf.Max(0f, Time.unscaledTime - activeStatusStartedAt);
        float remaining = Mathf.Max(0f, activeStatusUntil - Time.unscaledTime);
        float fadeIn = Mathf.Clamp01(age / 0.16f);
        float fadeOut = Mathf.Clamp01(remaining / 0.38f);
        statusFeedGroup.alpha = Mathf.Min(fadeIn, fadeOut);

        float pulse = Mathf.PingPong(Time.unscaledTime * (activeStatusPriority == RoundMessagePriority.Danger ? 5f : 2.7f), 1f);
        Color accent = activeStatusAccentColor;

        if (statusFeedBackground != null)
        {
            Color baseColor = new Color(accent.r * 0.14f, accent.g * 0.07f, accent.b * 0.06f, 0.9f);
            Color pulseColor = new Color(accent.r * 0.24f, accent.g * 0.11f, accent.b * 0.08f, 0.94f);
            statusFeedBackground.color = Color.Lerp(baseColor, pulseColor, pulse);
        }

        if (statusFeedAccent != null)
            statusFeedAccent.color = Color.Lerp(accent, new Color(1f, 0.86f, 0.42f, 1f), pulse * 0.35f);

        statusFeedTitleText.color = Color.Lerp(new Color(1f, 0.56f, 0.36f, 1f), new Color(1f, 0.82f, 0.48f, 1f), pulse);
        statusFeedLabelText.color = new Color(1f, 0.96f, 0.9f, 1f);
    }

    void ShowLeftFeedInternal(string title, string label, Sprite icon, float seconds)
    {
        EnsureUi();

        float now = Time.unscaledTime;
        leftFeedMessages.Insert(0, new LeftFeedMessage
        {
            Title = string.IsNullOrWhiteSpace(title) ? "LOOT" : title,
            Label = label ?? string.Empty,
            Icon = icon,
            CreatedAt = now,
            ExpiresAt = now + Mathf.Max(0.8f, seconds)
        });

        while (leftFeedMessages.Count > MaxLeftFeedMessages)
            leftFeedMessages.RemoveAt(leftFeedMessages.Count - 1);

        UpdateLeftFeedVisual();
    }

    void UpdateLeftFeedVisual()
    {
        if (leftFeedViews == null)
            return;

        float now = Time.unscaledTime;
        for (int i = leftFeedMessages.Count - 1; i >= 0; i--)
        {
            if (now >= leftFeedMessages[i].ExpiresAt)
                leftFeedMessages.RemoveAt(i);
        }

        for (int i = 0; i < leftFeedViews.Length; i++)
        {
            LeftFeedView view = leftFeedViews[i];
            if (view == null || view.RootObject == null)
                continue;

            if (i >= leftFeedMessages.Count)
            {
                view.Group.alpha = 0f;
                view.RootObject.SetActive(false);
                continue;
            }

            LeftFeedMessage message = leftFeedMessages[i];
            ApplyLeftFeedView(view, message, i, now);
        }
    }

    void ApplyLeftFeedView(LeftFeedView view, LeftFeedMessage message, int index, float now)
    {
        bool newest = index == 0;
        float age = Mathf.Max(0f, now - message.CreatedAt);
        float remaining = Mathf.Max(0f, message.ExpiresAt - now);
        float fadeIn = Mathf.Clamp01(age / 0.16f);
        float fadeOut = Mathf.Clamp01(remaining / 0.38f);
        float olderAlpha = newest ? 1f : index == 1 ? 0.76f : 0.58f;

        view.RootObject.SetActive(true);
        view.RootObject.transform.SetAsLastSibling();
        view.Group.alpha = Mathf.Min(fadeIn, fadeOut) * olderAlpha;

        view.Rect.anchoredPosition = new Vector2(0f, newest ? 0f : -98f - ((index - 1) * 74f));
        view.Rect.sizeDelta = newest ? new Vector2(300f, 104f) : new Vector2(286f, 72f);

        if (view.Background != null)
            view.Background.color = newest
                ? new Color(0.045f, 0.08f, 0.13f, 0.9f)
                : new Color(0.035f, 0.06f, 0.1f, 0.78f);

        if (view.TitleText != null)
        {
            view.TitleText.text = message.Title;
            view.TitleText.fontSize = newest ? 18f : 15f;
        }

        if (view.LabelText != null)
        {
            view.LabelText.text = message.Label;
            view.LabelText.fontSizeMax = newest ? 20f : 17f;
            view.LabelText.fontSizeMin = newest ? 17f : 14f;
        }

        if (view.Icon != null)
        {
            view.Icon.sprite = message.Icon;
            view.Icon.enabled = message.Icon != null;
            RectTransform iconRect = view.Icon.rectTransform;
            iconRect.anchoredPosition = newest ? new Vector2(48f, -16f) : new Vector2(40f, -11f);
            iconRect.sizeDelta = newest ? new Vector2(58f, 58f) : new Vector2(44f, 44f);
        }
    }

    static Color GetDefaultStatusAccent(RoundMessagePriority priority)
    {
        if (priority == RoundMessagePriority.Danger)
            return new Color(1f, 0.2f, 0.1f, 1f);

        if (priority == RoundMessagePriority.Event)
            return new Color(0.2f, 0.88f, 1f, 1f);

        return new Color(1f, 0.36f, 0.16f, 1f);
    }
}
