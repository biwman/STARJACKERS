using System.Text;
using System.Threading.Tasks;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EarlyRoundExitUI : MonoBehaviour
{
    const string ExitButtonLabel = "EXIT";
    const string ExitButtonPendingLabel = "EXITING...";

    static EarlyRoundExitUI instance;
    static bool buttonRequested;
    static bool summaryRequested;
    static int cachedFinalScore;
    static string cachedOutcome = "dead";
    static bool awardRequested;
    static bool exitRequested;

    GameObject buttonObject;
    GameObject summaryObject;
    TextMeshProUGUI summaryTitleText;
    TextMeshProUGUI summarySubtitleText;
    TextMeshProUGUI summaryScoreText;
    TextMeshProUGUI summaryOutcomeText;
    TextMeshProUGUI summaryListText;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void ShowEndRoundButton(int finalScore, string outcome)
    {
        EnsureInstance();
        cachedFinalScore = Mathf.Max(0, finalScore);
        cachedOutcome = string.IsNullOrWhiteSpace(outcome) ? "dead" : outcome;
        buttonRequested = true;
        summaryRequested = false;
        awardRequested = false;
        exitRequested = false;
        instance.Refresh();
    }

    public static void ShowFinishedRoundSummary(int finalScore, string outcome)
    {
        EnsureInstance();
        cachedFinalScore = Mathf.Max(0, finalScore);
        cachedOutcome = string.IsNullOrWhiteSpace(outcome) ? "finished" : outcome;
        buttonRequested = false;
        summaryRequested = true;
        awardRequested = false;
        exitRequested = false;
        NetworkManager.MarkCurrentRoundEndedForLocalPlayer(cachedOutcome);
        instance.Refresh();
        _ = instance.AwardRoundXpIfNeeded();
    }

    public static void HideAll()
    {
        buttonRequested = false;
        summaryRequested = false;
        exitRequested = false;
        if (instance != null)
            instance.Refresh();
    }

    static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("EarlyRoundExitUI");
        instance = root.AddComponent<EarlyRoundExitUI>();
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
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this)
            instance = null;
    }

    void Update()
    {
        Refresh();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Refresh();
    }

    void Refresh()
    {
        if (!ShouldRemainVisible())
        {
            buttonRequested = false;
            summaryRequested = false;
            exitRequested = false;
            if (buttonObject != null)
                buttonObject.SetActive(false);
            if (summaryObject != null)
                summaryObject.SetActive(false);
            return;
        }

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
            return;

        if (buttonRequested)
            EnsureButton(canvas.transform);

        if (summaryRequested)
        {
            EnsureSummary(canvas.transform);
            UpdateSummaryText();
        }

        if (buttonObject != null)
        {
            buttonObject.SetActive(buttonRequested && !summaryRequested);
            if (buttonRequested && !summaryRequested)
                UpdateExitButtonState();
        }

        if (summaryObject != null)
        {
            summaryObject.SetActive(summaryRequested);
            if (summaryRequested)
                RoundPilotHudUI.HideAllRuntimeObjects();
        }
    }

    bool ShouldRemainVisible()
    {
        if (!buttonRequested && !summaryRequested)
            return false;

        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return false;

        if (RoomSettings.GetSessionState() != RoomSettings.SessionStateInPlay)
            return false;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) ||
            value is not bool started ||
            !started)
        {
            return false;
        }

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.RoundResultsKey, out object snapshotValue) &&
            snapshotValue is string snapshot &&
            !string.IsNullOrWhiteSpace(snapshot))
        {
            return false;
        }

        return true;
    }

    void EnsureButton(Transform parent)
    {
        if (buttonObject != null && buttonObject.scene.IsValid())
        {
            if (buttonObject.transform.parent != parent)
                buttonObject.transform.SetParent(parent, false);

            TextMeshProUGUI existingText = buttonObject.GetComponentInChildren<TextMeshProUGUI>(true);
            if (existingText != null)
            {
                existingText.text = exitRequested ? ExitButtonPendingLabel : ExitButtonLabel;
                existingText.fontSize = exitRequested ? 20f : 26f;
            }

            UpdateExitButtonState();

            buttonObject.transform.SetAsLastSibling();
            return;
        }

        buttonObject = new GameObject("EarlyEndRoundButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-28f, -112f);
        rect.sizeDelta = new Vector2(230f, 58f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.48f, 0.08f, 0.09f, 0.96f);
        image.type = Image.Type.Sliced;

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnEndRoundClicked);

        TextMeshProUGUI text = CreateText(buttonObject.transform, "EarlyEndRoundButtonText", ExitButtonLabel, 26f, TextAlignmentOptions.Center);
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = 1.5f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        UpdateExitButtonState();
    }

    void UpdateExitButtonState()
    {
        if (buttonObject == null)
            return;

        Button button = buttonObject.GetComponent<Button>();
        if (button != null)
            button.interactable = !exitRequested;

        TextMeshProUGUI text = buttonObject.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            text.text = exitRequested ? ExitButtonPendingLabel : ExitButtonLabel;
            text.fontSize = exitRequested ? 20f : 26f;
        }
    }

    void EnsureSummary(Transform parent)
    {
        if (summaryObject != null && summaryObject.scene.IsValid())
        {
            if (summaryObject.transform.parent != parent)
                summaryObject.transform.SetParent(parent, false);

            summaryObject.transform.SetAsLastSibling();
            CacheSummaryText();
            return;
        }

        summaryObject = new GameObject("EarlyRoundSummaryPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        summaryObject.transform.SetParent(parent, false);

        RectTransform rootRect = summaryObject.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image rootImage = summaryObject.GetComponent<Image>();
        rootImage.color = new Color(0.01f, 0.018f, 0.03f, 0.68f);
        rootImage.raycastTarget = true;

        GameObject cardObject = new GameObject("EarlyRoundSummaryCard", typeof(RectTransform), typeof(Image));
        cardObject.transform.SetParent(summaryObject.transform, false);
        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(860f, 580f);

        Image cardImage = cardObject.GetComponent<Image>();
        cardImage.color = new Color(0.035f, 0.055f, 0.082f, 0.98f);
        cardImage.type = Image.Type.Sliced;

        GameObject accentObject = new GameObject("EarlyRoundSummaryAccent", typeof(RectTransform), typeof(Image));
        accentObject.transform.SetParent(cardObject.transform, false);
        RectTransform accentRect = accentObject.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(0f, 8f);
        accentObject.GetComponent<Image>().color = new Color(0.08f, 0.62f, 0.78f, 1f);

        summaryTitleText = CreateText(cardObject.transform, "EarlyRoundSummaryTitle", BuildSummaryTitle(), 42f, TextAlignmentOptions.Left);
        summaryTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        summaryTitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
        summaryTitleText.rectTransform.pivot = new Vector2(0.5f, 1f);
        summaryTitleText.rectTransform.anchoredPosition = new Vector2(0f, -32f);
        summaryTitleText.rectTransform.sizeDelta = new Vector2(-96f, 54f);
        summaryTitleText.color = new Color(0.92f, 0.98f, 1f, 1f);
        summaryTitleText.fontStyle = FontStyles.Bold;
        summaryTitleText.characterSpacing = 1.6f;

        summarySubtitleText = CreateText(cardObject.transform, "EarlyRoundSummarySubtitle", BuildSummarySubtitle(), 18f, TextAlignmentOptions.Left);
        summarySubtitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        summarySubtitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
        summarySubtitleText.rectTransform.pivot = new Vector2(0.5f, 1f);
        summarySubtitleText.rectTransform.anchoredPosition = new Vector2(0f, -84f);
        summarySubtitleText.rectTransform.sizeDelta = new Vector2(-96f, 26f);
        summarySubtitleText.color = new Color(0.68f, 0.78f, 0.88f, 0.95f);
        summarySubtitleText.fontStyle = FontStyles.Normal;

        GameObject scoreBadge = CreateSummaryPanel(cardObject.transform, "EarlyRoundSummaryScoreBadge", new Color(0.08f, 0.15f, 0.19f, 0.96f));
        RectTransform scoreBadgeRect = scoreBadge.GetComponent<RectTransform>();
        scoreBadgeRect.anchorMin = new Vector2(0f, 1f);
        scoreBadgeRect.anchorMax = new Vector2(0f, 1f);
        scoreBadgeRect.pivot = new Vector2(0f, 1f);
        scoreBadgeRect.anchoredPosition = new Vector2(48f, -124f);
        scoreBadgeRect.sizeDelta = new Vector2(360f, 92f);

        TextMeshProUGUI scoreLabel = CreateText(scoreBadge.transform, "EarlyRoundSummaryScoreLabel", "ROUND XP", 16f, TextAlignmentOptions.Left);
        scoreLabel.rectTransform.offsetMin = new Vector2(22f, 52f);
        scoreLabel.rectTransform.offsetMax = new Vector2(-20f, -14f);
        scoreLabel.color = new Color(0.56f, 0.76f, 0.86f, 1f);
        scoreLabel.fontStyle = FontStyles.Bold;
        scoreLabel.characterSpacing = 1.2f;

        summaryScoreText = CreateText(scoreBadge.transform, "EarlyRoundSummaryScoreValue", BuildScoreText(), 34f, TextAlignmentOptions.Left);
        summaryScoreText.rectTransform.offsetMin = new Vector2(20f, 10f);
        summaryScoreText.rectTransform.offsetMax = new Vector2(-20f, -34f);
        summaryScoreText.color = new Color(1f, 0.9f, 0.52f, 1f);
        summaryScoreText.fontStyle = FontStyles.Bold;

        GameObject outcomeBadge = CreateSummaryPanel(cardObject.transform, "EarlyRoundSummaryOutcomeBadge", new Color(0.095f, 0.11f, 0.15f, 0.96f));
        RectTransform outcomeBadgeRect = outcomeBadge.GetComponent<RectTransform>();
        outcomeBadgeRect.anchorMin = new Vector2(1f, 1f);
        outcomeBadgeRect.anchorMax = new Vector2(1f, 1f);
        outcomeBadgeRect.pivot = new Vector2(1f, 1f);
        outcomeBadgeRect.anchoredPosition = new Vector2(-48f, -124f);
        outcomeBadgeRect.sizeDelta = new Vector2(360f, 92f);

        TextMeshProUGUI outcomeLabel = CreateText(outcomeBadge.transform, "EarlyRoundSummaryOutcomeLabel", "STATUS", 16f, TextAlignmentOptions.Left);
        outcomeLabel.rectTransform.offsetMin = new Vector2(22f, 52f);
        outcomeLabel.rectTransform.offsetMax = new Vector2(-20f, -14f);
        outcomeLabel.color = new Color(0.56f, 0.76f, 0.86f, 1f);
        outcomeLabel.fontStyle = FontStyles.Bold;
        outcomeLabel.characterSpacing = 1.2f;

        summaryOutcomeText = CreateText(outcomeBadge.transform, "EarlyRoundSummaryOutcomeValue", BuildOutcomeText(), 28f, TextAlignmentOptions.Left);
        summaryOutcomeText.rectTransform.offsetMin = new Vector2(20f, 10f);
        summaryOutcomeText.rectTransform.offsetMax = new Vector2(-20f, -34f);
        summaryOutcomeText.color = GetOutcomeColor();
        summaryOutcomeText.fontStyle = FontStyles.Bold;

        GameObject listPanel = CreateSummaryPanel(cardObject.transform, "EarlyRoundSummaryListPanel", new Color(0.012f, 0.019f, 0.028f, 0.92f));
        RectTransform listPanelRect = listPanel.GetComponent<RectTransform>();
        listPanelRect.anchorMin = new Vector2(0f, 0f);
        listPanelRect.anchorMax = new Vector2(1f, 1f);
        listPanelRect.pivot = new Vector2(0.5f, 0.5f);
        listPanelRect.offsetMin = new Vector2(48f, 132f);
        listPanelRect.offsetMax = new Vector2(-48f, -238f);

        summaryListText = CreateText(listPanel.transform, "EarlyRoundSummaryList", BuildSummaryText(), 23f, TextAlignmentOptions.Left);
        summaryListText.rectTransform.anchorMin = new Vector2(0f, 0f);
        summaryListText.rectTransform.anchorMax = new Vector2(1f, 1f);
        summaryListText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        summaryListText.rectTransform.offsetMin = new Vector2(28f, 22f);
        summaryListText.rectTransform.offsetMax = new Vector2(-28f, -22f);
        summaryListText.color = new Color(0.86f, 0.94f, 1f, 1f);
        summaryListText.fontStyle = FontStyles.Bold;
        summaryListText.textWrappingMode = TextWrappingModes.NoWrap;
        summaryListText.overflowMode = TextOverflowModes.Ellipsis;
        summaryListText.lineSpacing = 8f;

        Button backButton = CreateButton(cardObject.transform, "EarlyRoundSummaryBackButton", "BACK", new Vector2(0f, 24f), new Vector2(340f, 86f), OnBackClicked);
        Image backImage = backButton.GetComponent<Image>();
        if (backImage != null)
        {
            backImage.color = new Color(0.08f, 0.42f, 0.5f, 0.98f);
            backImage.type = Image.Type.Sliced;
        }
        ApplyButtonColors(backButton, new Color(0.08f, 0.42f, 0.5f, 0.98f), new Color(0.12f, 0.58f, 0.68f, 1f), new Color(0.05f, 0.28f, 0.34f, 1f));

        TextMeshProUGUI backText = backButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (backText != null)
        {
            backText.fontSize = 34f;
            backText.characterSpacing = 1.6f;
        }
    }

    void CacheSummaryText()
    {
        if (summaryObject == null)
            return;

        summaryTitleText = FindSummaryText("EarlyRoundSummaryCard/EarlyRoundSummaryTitle");
        summarySubtitleText = FindSummaryText("EarlyRoundSummaryCard/EarlyRoundSummarySubtitle");
        summaryScoreText = FindSummaryText("EarlyRoundSummaryCard/EarlyRoundSummaryScoreBadge/EarlyRoundSummaryScoreValue");
        summaryOutcomeText = FindSummaryText("EarlyRoundSummaryCard/EarlyRoundSummaryOutcomeBadge/EarlyRoundSummaryOutcomeValue");

        Transform textTransform = summaryObject.transform.Find("EarlyRoundSummaryCard/EarlyRoundSummaryListPanel/EarlyRoundSummaryList");
        if (textTransform == null)
            textTransform = summaryObject.transform.Find("EarlyRoundSummaryCard/EarlyRoundSummaryList");
        if (textTransform != null)
            summaryListText = textTransform.GetComponent<TextMeshProUGUI>();
    }

    void UpdateSummaryText()
    {
        CacheSummaryText();
        if (summaryTitleText != null)
            summaryTitleText.text = BuildSummaryTitle();
        if (summarySubtitleText != null)
            summarySubtitleText.text = BuildSummarySubtitle();
        if (summaryScoreText != null)
            summaryScoreText.text = BuildScoreText();
        if (summaryOutcomeText != null)
        {
            summaryOutcomeText.text = BuildOutcomeText();
            summaryOutcomeText.color = GetOutcomeColor();
        }
        if (summaryListText != null)
            summaryListText.text = BuildSummaryText();
    }

    TextMeshProUGUI FindSummaryText(string path)
    {
        if (summaryObject == null)
            return null;

        Transform target = summaryObject.transform.Find(path);
        return target != null ? target.GetComponent<TextMeshProUGUI>() : null;
    }

    string BuildSummaryTitle()
    {
        switch ((cachedOutcome ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "dead":
                return "SHIP LOST";
            case "lost_in_space":
            case "time_up":
                return "LOST IN SPACE";
            case "extracted":
            case "evacuated":
                return "EXTRACTION COMPLETE";
            default:
                return "ROUND COMPLETE";
        }
    }

    string BuildSummarySubtitle()
    {
        string name = PhotonNetwork.LocalPlayer != null && !string.IsNullOrWhiteSpace(PhotonNetwork.LocalPlayer.NickName)
            ? PhotonNetwork.LocalPlayer.NickName
            : "Pilot";

        return name + " - final round report";
    }

    string BuildScoreText()
    {
        return Mathf.Max(0, cachedFinalScore) + " XP";
    }

    string BuildOutcomeText()
    {
        return FormatOutcome(cachedOutcome).ToUpperInvariant();
    }

    Color GetOutcomeColor()
    {
        switch ((cachedOutcome ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "dead":
            case "lost_in_space":
            case "time_up":
                return new Color(1f, 0.42f, 0.32f, 1f);
            case "extracted":
            case "evacuated":
                return new Color(0.42f, 1f, 0.68f, 1f);
            default:
                return new Color(0.9f, 0.96f, 1f, 1f);
        }
    }

    string BuildSummaryLine()
    {
        string name = PhotonNetwork.LocalPlayer != null && !string.IsNullOrWhiteSpace(PhotonNetwork.LocalPlayer.NickName)
            ? PhotonNetwork.LocalPlayer.NickName
            : "Player";

        return name + " - " + Mathf.Max(0, cachedFinalScore) + " XP - " + cachedOutcome;
    }

    string BuildSummaryText()
    {
        RoundResultsSnapshotData snapshot = RoundResultsTracker.BuildFinishedSnapshotForEarlySummary(
            PhotonNetwork.LocalPlayer,
            cachedFinalScore,
            cachedOutcome);

        if (snapshot == null || snapshot.entries == null || snapshot.entries.Length == 0)
            return BuildSummaryLine();

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < snapshot.entries.Length; i++)
        {
            RoundResultEntry entry = snapshot.entries[i];
            if (entry == null)
                continue;

            int placement = entry.placement > 0 ? entry.placement : i + 1;
            string nickname = string.IsNullOrWhiteSpace(entry.nickname) ? "Player " + entry.actorNumber : entry.nickname;
            builder.Append('#').Append(placement)
                .Append("   ")
                .Append(nickname)
                .Append("     ")
                .Append(Mathf.Max(0, entry.finalScore))
                .Append(" XP     ")
                .Append(FormatOutcome(entry.outcome).ToUpperInvariant());

            if (i < snapshot.entries.Length - 1)
                builder.AppendLine();
        }

        return builder.Length > 0 ? builder.ToString() : BuildSummaryLine();
    }

    string FormatOutcome(string outcome)
    {
        if (string.IsNullOrWhiteSpace(outcome))
            return "finished";

        switch (outcome.Trim().ToLowerInvariant())
        {
            case "dead":
                return "destroyed";
            case "lost_in_space":
                return "lost in space";
            case "time_up":
                return "time up";
            case "extracted":
                return "extracted";
            case "evacuated":
                return "evacuated";
            default:
                return outcome;
        }
    }

    async void OnEndRoundClicked()
    {
        if (exitRequested)
            return;

        exitRequested = true;
        UpdateExitButtonState();
        NetworkManager.MarkCurrentRoundEndedForLocalPlayer(cachedOutcome);
        await AwardRoundXpIfNeeded();
        HideAll();
        NetworkManager.ReturnToSessionBrowserFromFinishedRound();
    }

    void OnBackClicked()
    {
        if (string.Equals(cachedOutcome, "extracted", System.StringComparison.OrdinalIgnoreCase))
            AudioManager.Instance.RequestShipReturnMusicForNextMenu();

        HideAll();
        NetworkManager.ReturnToSessionBrowserFromFinishedRound();
    }

    async Task AwardRoundXpIfNeeded()
    {
        if (awardRequested)
            return;

        awardRequested = true;
        string matchToken = BuildMatchToken();
        try
        {
            await PlayerProfileService.Instance.RecordRoundXpAsync(cachedFinalScore, matchToken);
            await PlayerProfileService.Instance.RecordMapSuccessfulReturnAsync(RoomSettings.GetSelectedLobbyMapId(), cachedOutcome, matchToken);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("EarlyRoundExitUI: failed to record early round progress: " + ex);
        }
    }

    string BuildMatchToken()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return "early_round_" + Time.realtimeSinceStartup.ToString("F2");

        string startValue = "nostart";
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.StartTimeKey, out object value) && value != null)
            startValue = value.ToString();

        return PhotonNetwork.CurrentRoom.Name + "_" + startValue;
    }

    Button CreateButton(Transform parent, string objectName, string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(onClick);
        ApplyButtonColors(button, new Color(0.08f, 0.42f, 0.5f, 0.98f), new Color(0.12f, 0.58f, 0.68f, 1f), new Color(0.05f, 0.28f, 0.34f, 1f));

        TextMeshProUGUI text = CreateText(buttonObject.transform, objectName + "Text", label, 24f, TextAlignmentOptions.Center);
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;
        return button;
    }

    GameObject CreateSummaryPanel(Transform parent, string objectName, Color color)
    {
        GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);

        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;
        return panel;
    }

    void ApplyButtonColors(Button button, Color normal, Color highlighted, Color pressed)
    {
        if (button == null)
            return;

        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = highlighted;
        colors.pressedColor = pressed;
        colors.selectedColor = highlighted;
        colors.disabledColor = new Color(normal.r, normal.g, normal.b, 0.42f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    TextMeshProUGUI CreateText(Transform parent, string objectName, string value, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;

        TMP_Text reference = FindAnyObjectByType<TextMeshProUGUI>();
        if (reference != null)
        {
            text.font = reference.font;
            text.fontSharedMaterial = reference.fontSharedMaterial;
        }

        return text;
    }
}

public sealed class AstronautKillMeButtonUI : MonoBehaviour
{
    const string RootName = "AstronautKillMeButtonUI";
    const string ButtonName = "AstronautKillMeButton";
    const string ButtonTextName = "AstronautKillMeButtonText";
    static readonly Color NormalColor = new Color(0.45f, 0.04f, 0.08f, 0.98f);
    static readonly Color HighlightedColor = new Color(0.62f, 0.07f, 0.12f, 1f);
    static readonly Color PressedColor = new Color(0.29f, 0.02f, 0.05f, 1f);

    static AstronautKillMeButtonUI instance;

    GameObject buttonObject;
    Button killButton;
    TextMeshProUGUI buttonText;
    PlayerHealth targetHealth;
    int pendingRequestViewId = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

    static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject(RootName);
        instance = root.AddComponent<AstronautKillMeButtonUI>();
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
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this)
            instance = null;
    }

    void Update()
    {
        Refresh();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        targetHealth = null;
        pendingRequestViewId = -1;
        Refresh();
    }

    void Refresh()
    {
        if (!ShouldShowDuringRound())
        {
            if (buttonObject != null)
                buttonObject.SetActive(false);

            targetHealth = null;
            pendingRequestViewId = -1;
            return;
        }

        targetHealth = ResolveLocalAstronaut();
        if (targetHealth == null)
        {
            if (buttonObject != null)
                buttonObject.SetActive(false);

            pendingRequestViewId = -1;
            return;
        }

        Canvas canvas = ResolveCanvas();
        if (canvas == null)
            return;

        EnsureButton(canvas.transform);
        buttonObject.SetActive(true);
        buttonObject.transform.SetAsLastSibling();

        int viewId = targetHealth.photonView != null ? targetHealth.photonView.ViewID : -1;
        if (pendingRequestViewId != viewId)
            pendingRequestViewId = -1;

        if (killButton != null)
            killButton.interactable = pendingRequestViewId < 0 && targetHealth.CanRequestAstronautKillMeLocally();
    }

    PlayerHealth ResolveLocalAstronaut()
    {
        if (PhotonNetwork.LocalPlayer != null && PhotonNetwork.LocalPlayer.TagObject is GameObject taggedObject)
        {
            PlayerHealth taggedHealth = taggedObject.GetComponent<PlayerHealth>();
            if (IsUsableLocalAstronaut(taggedHealth))
                return taggedHealth;
        }

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            if (IsUsableLocalAstronaut(players[i]))
                return players[i];
        }

        return null;
    }

    bool IsUsableLocalAstronaut(PlayerHealth health)
    {
        if (health == null || health.photonView == null || !health.photonView.IsMine)
            return false;

        return health.CanRequestAstronautKillMeLocally();
    }

    bool ShouldShowDuringRound()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return false;

        if (RoomSettings.GetSessionState() != RoomSettings.SessionStateInPlay)
            return false;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) ||
            value is not bool started ||
            !GameplayHudVisibility.IsGameplayHudVisible(started))
        {
            return false;
        }

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.RoundResultsKey, out object snapshotValue) &&
            snapshotValue is string snapshot &&
            !string.IsNullOrWhiteSpace(snapshot))
        {
            return false;
        }

        return true;
    }

    Canvas ResolveCanvas()
    {
        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject != null)
        {
            Canvas namedCanvas = canvasObject.GetComponent<Canvas>();
            if (namedCanvas != null)
                return namedCanvas;
        }

        return FindAnyObjectByType<Canvas>();
    }

    void EnsureButton(Transform parent)
    {
        if (buttonObject != null && buttonObject.scene.IsValid())
        {
            if (buttonObject.transform.parent != parent)
                buttonObject.transform.SetParent(parent, false);

            CacheButtonReferences();
            ApplyButtonStyle();
            return;
        }

        buttonObject = new GameObject(ButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-22f, -74f);
        rect.sizeDelta = new Vector2(176f, 54f);

        GameObject textObject = new GameObject(ButtonTextName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        buttonText = textObject.GetComponent<TextMeshProUGUI>();
        buttonText.raycastTarget = false;

        TMP_Text reference = FindAnyObjectByType<TMP_Text>();
        if (reference != null)
        {
            buttonText.font = reference.font;
            buttonText.fontSharedMaterial = reference.fontSharedMaterial;
        }

        killButton = buttonObject.GetComponent<Button>();
        killButton.onClick.RemoveAllListeners();
        killButton.onClick.AddListener(OnKillMeClicked);
        ApplyButtonStyle();
    }

    void CacheButtonReferences()
    {
        if (buttonObject == null)
            return;

        killButton = buttonObject.GetComponent<Button>();
        buttonText = buttonObject.GetComponentInChildren<TextMeshProUGUI>(true);
        if (killButton != null)
        {
            killButton.onClick.RemoveListener(OnKillMeClicked);
            killButton.onClick.AddListener(OnKillMeClicked);
        }
    }

    void ApplyButtonStyle()
    {
        if (buttonObject == null)
            return;

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-22f, -74f);
            rect.sizeDelta = new Vector2(176f, 54f);
        }

        Image image = buttonObject.GetComponent<Image>();
        if (image != null)
        {
            image.color = NormalColor;
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
        }

        if (killButton != null)
        {
            killButton.targetGraphic = image;
            ColorBlock colors = killButton.colors;
            colors.normalColor = NormalColor;
            colors.highlightedColor = HighlightedColor;
            colors.pressedColor = PressedColor;
            colors.selectedColor = HighlightedColor;
            colors.disabledColor = new Color(NormalColor.r, NormalColor.g, NormalColor.b, 0.42f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            killButton.colors = colors;
        }

        if (buttonText != null)
        {
            buttonText.text = "KILL ME";
            buttonText.fontSize = 22f;
            buttonText.fontStyle = FontStyles.Bold;
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.color = Color.white;
            buttonText.characterSpacing = 1.2f;
            buttonText.textWrappingMode = TextWrappingModes.NoWrap;
            buttonText.overflowMode = TextOverflowModes.Ellipsis;
            buttonText.raycastTarget = false;
        }
    }

    void OnKillMeClicked()
    {
        PlayerHealth health = targetHealth != null ? targetHealth : ResolveLocalAstronaut();
        if (health == null || !health.CanRequestAstronautKillMeLocally())
            return;

        pendingRequestViewId = health.photonView != null ? health.photonView.ViewID : -1;
        if (killButton != null)
            killButton.interactable = false;

        health.RequestLocalAstronautKillMe();
    }
}
